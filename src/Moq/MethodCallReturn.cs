﻿//Copyright (c) 2007. Clarius Consulting, Manas Technology Solutions, InSTEDD
//https://github.com/moq/moq4
//All rights reserved.

//Redistribution and use in source and binary forms, 
//with or without modification, are permitted provided 
//that the following conditions are met:

//    * Redistributions of source code must retain the 
//    above copyright notice, this list of conditions and 
//    the following disclaimer.

//    * Redistributions in binary form must reproduce 
//    the above copyright notice, this list of conditions 
//    and the following disclaimer in the documentation 
//    and/or other materials provided with the distribution.

//    * Neither the name of Clarius Consulting, Manas Technology Solutions or InSTEDD nor the 
//    names of its contributors may be used to endorse 
//    or promote products derived from this software 
//    without specific prior written permission.

//THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
//CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
//INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
//MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
//DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR 
//CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
//SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
//BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
//SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
//INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
//WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
//NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
//OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
//SUCH DAMAGE.

//[This is the BSD license, see
// http://www.opensource.org/licenses/bsd-license.php]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

using Moq.Language;
using Moq.Language.Flow;
using Moq.Properties;

namespace Moq
{
	internal sealed partial class MethodCallReturn<TMock, TResult> : MethodCall, ISetup<TMock, TResult>, ISetupGetter<TMock, TResult>, IReturnsResult<TMock>
		where TMock : class
	{
		// This enum exists for reasons of optimizing memory usage.
		// Previously this class had two `bool` fields, `hasReturnValue` and `callBase`.
		// Using an enum allows us to combine them into a single field.
		private enum ReturnValueKind : byte
		{
			None = 0,
			Explicit,
			CallBase,
		}

		private Delegate valueDel;
		private Action<object[]> afterReturnCallback;
		private ReturnValueKind returnValueKind;

		public MethodCallReturn(Mock mock, Condition condition, LambdaExpression originalExpression, MethodInfo method, IReadOnlyList<Expression> arguments)
			: base(mock, condition, originalExpression, method, arguments)
		{
		}

		public bool ProvidesReturnValue() => this.returnValueKind != ReturnValueKind.None;

		public IVerifies Raises(Action<TMock> eventExpression, EventArgs args)
		{
			return this.Raises(eventExpression, () => args);
		}

		public IVerifies Raises(Action<TMock> eventExpression, Func<EventArgs> func)
		{
			return this.RaisesImpl(eventExpression, func);
		}

		public IVerifies Raises(Action<TMock> eventExpression, params object[] args)
		{
			return this.RaisesImpl(eventExpression, args);
		}

		public IReturnsResult<TMock> Returns(Delegate valueFunction)
		{
			// If `TResult` is `Delegate`, that is someone is setting up the return value of a method
			// that returns a `Delegate`, then we have arrived here because C# picked the wrong overload:
			// We don't want to invoke the passed delegate to get a return value; the passed delegate
			// already is the return value.
			if (typeof(TResult) == typeof(Delegate))
			{
				return this.Returns(() => (TResult)(object)valueFunction);
			}

			if (valueFunction != null && valueFunction.GetMethodInfo().ReturnType == typeof(void))
			{
				throw new ArgumentException(Resources.InvalidReturnsCallbackNotADelegateWithReturnType, nameof(valueFunction));
			}

			SetReturnDelegate(valueFunction);
			return this;
		}

		public IReturnsResult<TMock> Returns(Func<TResult> valueExpression)
		{
			SetReturnDelegate(valueExpression);
			return this;
		}

		public IReturnsResult<TMock> Returns(TResult value)
		{
			Returns(() => value);
			return this;
		}

		public IReturnsResult<TMock> CallBase()
		{
			this.returnValueKind = ReturnValueKind.CallBase;
			return this;
		}

		IReturnsThrows<TMock, TResult> ICallback<TMock, TResult>.Callback(Delegate callback)
		{
			base.Callback(callback);
			return this;
		}

		IReturnsThrowsGetter<TMock, TResult> ICallbackGetter<TMock, TResult>.Callback(Action callback)
		{
			base.Callback(callback);
			return this;
		}

		public new IReturnsThrows<TMock, TResult> Callback(Action callback)
		{
			base.Callback(callback);
			return this;
		}

		private void SetReturnDelegate(Delegate value)
		{
			if (value == null)
			{
				// A `null` reference (instead of a valid delegate) is interpreted as the actual return value.
				// This is necessary because the compiler might have picked the unexpected overload for calls like `Returns(null)`,
				// or the user might have picked an overload like `Returns<T>(null)`,
				// and instead of in `Returns(TResult)`, we ended up in `Returns(Delegate)` or `Returns(Func)`,
				// which likely isn't what the user intended.
				// So here we do what we would've done in `Returns(TResult)`:
				this.valueDel = (Func<TResult>)(() => default(TResult));
			}
			else
			{
				ValidateReturnDelegate(value);
				this.valueDel = value;
			}

			this.returnValueKind = ReturnValueKind.Explicit;
		}

		private void ValidateReturnDelegate(Delegate callback)
		{
			var callbackMethod = callback.GetMethodInfo();

			var expectedParams = this.Method.GetParameters();
			var actualParams = callback.GetMethodInfo().GetParameters();
			if (callback.HasCompatibleParameterList(expectedParams))
			{
				return;
			}

			// validate number of parameters:

			var numberOfActualParameters = callbackMethod.GetParameters().Length;
			if (callbackMethod.IsExtensionMethod())
			{
				numberOfActualParameters--;
			}

			if (numberOfActualParameters > 0)
			{
				var numberOfExpectedParameters = this.Method.GetParameters().Length;
				if (numberOfActualParameters != numberOfExpectedParameters)
				{
					throw new ArgumentException(
						string.Format(
							CultureInfo.CurrentCulture,
							Resources.InvalidCallbackParameterCountMismatch,
							numberOfExpectedParameters,
							numberOfActualParameters));
				}
			}

			// validate return type:

			var expectedReturnType = this.Method.ReturnType;
			var actualReturnType = callbackMethod.ReturnType;

			if (!expectedReturnType.IsAssignableFrom(actualReturnType))
			{
				throw new ArgumentException(
					string.Format(
						CultureInfo.CurrentCulture,
						Resources.InvalidCallbackReturnTypeMismatch,
						expectedReturnType,
						actualReturnType));
			}
		}

		protected override void SetCallbackWithoutArguments(Action callback)
		{
			if (this.ProvidesReturnValue())
			{
				this.afterReturnCallback = delegate { callback(); };
			}
			else
			{
				base.SetCallbackWithoutArguments(callback);
			}
		}

		protected override void SetCallbackWithArguments(Delegate callback)
		{
			if (this.ProvidesReturnValue())
			{
				this.afterReturnCallback = delegate(object[] args) { callback.InvokePreserveStack(args); };
			}
			else
			{
				base.SetCallbackWithArguments(callback);
			}
		}

		public override void Execute(Invocation invocation)
		{
			base.Execute(invocation);

			if (this.returnValueKind == ReturnValueKind.CallBase)
			{
				invocation.ReturnBase();
			}
			else if (this.valueDel != null)
			{
				invocation.Return(this.valueDel.HasCompatibleParameterList(new ParameterInfo[0])
					? valueDel.InvokePreserveStack()                //we need this, for the user to be able to use parameterless methods
					: valueDel.InvokePreserveStack(invocation.Arguments)); //will throw if parameters mismatch
			}
			else if (this.Mock.Behavior == MockBehavior.Strict)
			{
				throw MockException.ReturnValueRequired(invocation);
			}
			else
			{
				invocation.Return(default(TResult));
			}

			this.afterReturnCallback?.Invoke(invocation.Arguments);
		}
	}
}
