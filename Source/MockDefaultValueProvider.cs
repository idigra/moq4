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
using System.Diagnostics;
using System.Reflection;

namespace Moq
{
	/// <summary>
	/// A <see cref="IDefaultValueProvider"/> that returns an empty default value 
	/// for non-mockeable types, and mocks for all other types (interfaces and 
	/// non-sealed classes) that can be mocked.
	/// </summary>
	internal sealed class MockDefaultValueProvider : IDefaultValueProvider
	{
		public static MockDefaultValueProvider Instance { get; } = new MockDefaultValueProvider();

		private MockDefaultValueProvider()
		{
		}

		public object ProvideDefault(MethodInfo member, Mock mock)
		{
			var emptyValue = EmptyDefaultValueProvider.Instance.ProvideDefault(member, mock);
			if (emptyValue != null)
			{
				return emptyValue;
			}
			else if (member.ReturnType.IsMockeable())
			{
				var innerMock = mock.InnerMocks.GetOrAdd(member, info =>
				{
					// Create a new mock to be placed to InnerMocks dictionary if it's missing there
					var mockType = typeof(Mock<>).MakeGenericType(info.ReturnType);
					Mock newMock = (Mock)Activator.CreateInstance(mockType, mock.Behavior);
					newMock.DefaultValue = mock.DefaultValue;
					newMock.CallBase = mock.CallBase;
					newMock.Switches = mock.Switches;
					return newMock;
				});

				Debug.Assert(innerMock != null);
				return innerMock.Object;
			}
			else
			{
				return null;
			}
		}
	}
}
