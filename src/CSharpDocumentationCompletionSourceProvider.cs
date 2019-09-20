#region Copyright
/* Copyright 2019 Daniel Kopp
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.using System.Reflection;
 */
#endregion

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace NerdyDuck.CSharpDocumentationCompletion2019
{
	/// <summary>
	/// Instantiated by VS to provide IAsyncCompletionSource instances when auto-completion is triggered in C# documents.
	/// </summary>
	[Export(typeof(IAsyncCompletionSourceProvider))]
	[Name("XML Comments Completion Source Provider")]
	[ContentType("CSharp")]
	internal class CSharpDocumentationCompletionSourceProvider : IAsyncCompletionSourceProvider
	{
		// Service interfaces to be provided by VS.

		[Import]
		private IClassifierAggregatorService ClassifierService
		{
			get; set;
		}

		[Import]
		private ITextStructureNavigatorSelectorService NavigatorService
		{
			get; set;
		}

		[Import]
		private IEditorOperationsFactoryService OperationsFactory
		{
			get; set;
		}

		public IAsyncCompletionSource GetOrCreate(ITextView textView) => textView.Properties.GetOrCreateSingletonProperty(() => new CSharpDocumentationCompletionSource(textView, ClassifierService, NavigatorService, OperationsFactory));
	}
}
