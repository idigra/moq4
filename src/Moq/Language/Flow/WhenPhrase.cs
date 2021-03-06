// Copyright (c) 2007, Clarius Consulting, Manas Technology Solutions, InSTEDD.
// All rights reserved. Licensed under the BSD 3-Clause License; see License.txt.

using System;
using System.Linq.Expressions;

namespace Moq.Language.Flow
{
	internal sealed class WhenPhrase<T> : ISetupConditionResult<T>
		where T : class
	{
		private Mock<T> mock;
		private Condition condition;

		public WhenPhrase(Mock<T> mock, Condition condition)
		{
			this.mock = mock;
			this.condition = condition;
		}

		public ISetup<T> Setup(Expression<Action<T>> expression)
		{
			return Mock.Setup<T>(mock, expression, this.condition);
		}

		public ISetup<T, TResult> Setup<TResult>(Expression<Func<T, TResult>> expression)
		{
			return Mock.Setup<T, TResult>(mock, expression, this.condition);
		}

		public ISetupGetter<T, TProperty> SetupGet<TProperty>(Expression<Func<T, TProperty>> expression)
		{
			return Mock.SetupGet<T, TProperty>(mock, expression, this.condition);
		}

		public ISetupSetter<T, TProperty> SetupSet<TProperty>(Action<T> setterExpression)
		{
			return Mock.SetupSet<T, TProperty>(mock, setterExpression, this.condition);
		}

		public ISetup<T> SetupSet(Action<T> setterExpression)
		{
			return Mock.SetupSet<T>(mock, setterExpression, this.condition);
		}
	}
}
