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

using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace NerdyDuck.CSharpDocumentationCompletion2019
{
	class CSharpDocumentationCompletionSource : IAsyncCompletionSource
	{
		private readonly IClassifier _classifier;
		private readonly ITextStructureNavigator _navigator;
		private readonly ITextView _textView;
		private readonly ITextBuffer _textBuffer;
		private readonly IEditorOperationsFactoryService _operationsFactory;

		private static readonly ImageElement _iconSource = new ImageElement(new ImageId(KnownImageIds.ImageCatalogGuid, KnownImageIds.IntellisenseKeyword));
		private static readonly ImageElement _macroIconSource = new ImageElement(new ImageId(KnownImageIds.ImageCatalogGuid, KnownImageIds.MacroPublic));

		public CSharpDocumentationCompletionSource(ITextView textView, IClassifierAggregatorService classifier, ITextStructureNavigatorSelectorService navigator, IEditorOperationsFactoryService operationsFactory)
		{
			_textView = textView;
			_textBuffer = textView.TextBuffer;
			_classifier = classifier.GetClassifier(_textBuffer);
			_navigator = navigator.GetTextStructureNavigator(_textBuffer);
			_operationsFactory = operationsFactory;
		}

		/// <summary>
		/// Determine if this IAsyncCompletionSource wants to participate in the current auto-completion session.
		/// </summary>
		/// <param name="trigger">The action that triggered the auto-completion session.</param>
		/// <param name="triggerLocation">The location in the text where auto-completion was triggered.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>CompletionStartData.DoesNotParticipateInCompletion, if the current instance does not want to provide items for auto-completion; otherwise, an instance of CompletionStartData.</returns>
		public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken cancellationToken)
		{
			// Get the syntactical parts of the current line, and check if the trigger location is after the XML doc delimiter '///'
			IList<ClassificationSpan>  spans = _classifier.GetClassificationSpans(triggerLocation.GetContainingLine().Extent);
			ClassificationSpan commentDelimiter = spans.FirstOrDefault(s => s.ClassificationType.IsOfType(ClassificationTypeNames.XmlDocCommentDelimiter));
			if (commentDelimiter == null || commentDelimiter.Span.End > triggerLocation || cancellationToken.IsCancellationRequested)
			{
				// No XML comment in that line, or trigger location is before the '///' comment start.
				return CompletionStartData.DoesNotParticipateInCompletion;
			}

			// See if trigger location is within a text span
			int triggerSpanIndex = -1;
			for (int i = 0; i < spans.Count; i++)
			{
				if (spans[i].Span.Contains(triggerLocation))
				{
					triggerSpanIndex = i;
					break;
				}
				else if (cancellationToken.IsCancellationRequested)
				{
					return CompletionStartData.DoesNotParticipateInCompletion;
				}
			}

			if (triggerSpanIndex == -1) // Not within a word (EOL etc.)
			{
				if (trigger.Character == '<' || (trigger.Character == '\0' && (trigger.Reason == CompletionTriggerReason.Invoke || trigger.Reason == CompletionTriggerReason.InvokeAndCommitIfUnique))) 
				{
					SnapshotSpan applicableToSpan = _navigator.GetExtentOfWord(triggerLocation).Span;
					return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableToSpan);
				}
				else
				{
					return CompletionStartData.DoesNotParticipateInCompletion;
				}
			}
			else // Within a word
			{
				ClassificationSpan triggerSpan = spans[triggerSpanIndex];
				if (triggerSpan.ClassificationType.IsOfType(ClassificationTypeNames.XmlDocCommentText)) // Just text, so an xml element could be inserted here
				{
					if (trigger.Character == '<')
					{
						SnapshotSpan applicableToSpan = _navigator.GetExtentOfWord(triggerLocation).Span;
						return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableToSpan);
					}
				}
			}
			return CompletionStartData.DoesNotParticipateInCompletion;
		}

		/// <summary>
		/// Provide CompletionContext items for the current auto-completion session.
		/// </summary>
		/// <param name="session">The current auto-completion session.</param>
		/// <param name="trigger">The action that triggered the auto-completion session.</param>
		/// <param name="triggerLocation">The location in the text where auto-completion was triggered.</param>
		/// <param name="applicableToSpan">The text area that may be affected by the auto-completion.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task returning a CompletionContext, filled with CompletionItems, if applicable.</returns>
		public Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken) => Task.Run(() =>
		{

			bool addPrefix = false;
			if (trigger.Character == '\0' && !_navigator.GetExtentOfWord(triggerLocation.Subtract(1)).Span.GetText().EndsWith("<", System.StringComparison.OrdinalIgnoreCase))
			{
				// Meaning: autocomplete was not triggered by typing '<', but by using a shortcut, e.g. Ctrl+Space.
				// If the previous character is not '<', then we need to add one to the completion item text.
				addPrefix = true;
			}

			SnapshotSpan ats = applicableToSpan;
			SnapshotPoint t = triggerLocation;
			// TODO: Find a way to determine where in the comment section the completion was triggered, to filter out CompletionItems that are not applicable in the current context

			List<CompletionItem> completions = new List<CompletionItem>()
			{
				CreateCompletionItem(addPrefix, "conceptualLink", "conceptualLink target=\"\"/>", "This element is used to create a link to a MAML topic within the See Also section of a topic or an inline link to a MAML topic within one of the other XML comments elements.", false, 3),
				CreateCompletionItem(addPrefix, "inheritdoc", "inheritdoc/>", "This element can help minimize the effort required to document complex APIs by allowing common documentation to be inherited from base types/members."),
				CreateCompletionItem(addPrefix, "inheritdocCref", "inheritdoc cref=\"\"/>", "Inherit documentation from a specific member.", false, 3),
				CreateCompletionItem(addPrefix, "inheritdocCrefSelect", "inheritdoc cref=\"\" select=\"summary|remarks\"/>", "Inherit documentation from a specific member and comments.", false, 28),
				CreateCompletionItem(addPrefix, "token", "token", "This element represents a replaceable tag within a topic."),
				// exception
				CreateCompletionItem(addPrefix, "AttachedEventComments", "AttachedEventComments", "This element is used to define the content that should appear on the auto-generated attached event member topic for a given WPF routed event member."),
				CreateCompletionItem(addPrefix, "AttachedPropertyComments", "AttachedPropertyComments", "This element is used to define the content that should appear on the auto-generated attached property member topic for a given WPF dependency property member."),
				CreateCompletionItem(addPrefix, "event", "event cref=\"\"", "This element is used to list events that can be raised by a type's member.", false, 1),
				CreateCompletionItem(addPrefix, "overloads", "overloads", "This element is used to define the content that should appear on the auto-generated overloads topic for a given set of member overloads."),
				CreateCompletionItem(addPrefix, "preliminary", "preliminary/>", "This element is used to indicate that a particular type or member is preliminary and is subject to change."),
				CreateCompletionItem(addPrefix, "threadsafety", "threadsafety static=\"true\" instance=\"false\"/>", "This element is used to indicate whether or not a class or structure's static and instance members are safe for use in multi-threaded scenarios."),
				// list
				CreateCompletionItem(addPrefix, "note", "note type=\"note\"", "This element is used to create a note-like section within a topic to draw attention to some important information."),
				// language
				CreateCompletionItem(addPrefix, "null", "see langword=\"null\"/>", "Inserts the language-specific keyword 'null'.", true),
				CreateCompletionItem(addPrefix, "static", "see langword=\"static\"/>", "Inserts the language-specific keyword 'static'.", true),
				CreateCompletionItem(addPrefix, "virtual", "see langword=\"virtual\"/>", "Inserts the language-specific keyword 'virtual'.", true),
				CreateCompletionItem(addPrefix, "true", "see langword=\"true\"/>", "Inserts the language-specific keyword 'true'.", true),
				CreateCompletionItem(addPrefix, "false", "see langword=\"false\"/>", "Inserts the language-specific keyword 'false'.", true),
				CreateCompletionItem(addPrefix, "abstract", "see langword=\"abstract\"/>", "Inserts the language-specific keyword 'abstract'.", true),
				CreateCompletionItem(addPrefix, "sealed", "see langword=\"sealed\"/>", "Inserts the language-specific keyword 'sealed'.", true),
				CreateCompletionItem(addPrefix, "async", "see langword=\"async\"/>", "Inserts the language-specific keyword 'async'.", true),
				CreateCompletionItem(addPrefix, "await", "see langword=\"await\"/>", "Inserts the language-specific keyword 'await'.", true),
				CreateCompletionItem(addPrefix, "asyncAwait", "see langword=\"async/await\"/>", "Inserts the language-specific keyword 'async/await'.", true),
				// code
				CreateCompletionItem(addPrefix, "codeImport", "code language=\"\" title=\" \" source=\"..\\Path\\SourceFile.cs\" region=\"Region Name\"/>", "This element is used to indicate that a multi-line section of text should be imported from the named region of the named file and formatted as a code block.", false, 65),
				CreateCompletionItem(addPrefix, "codeLanguage", "code language=\"\" title=\" \"></code>", "This element is used to indicate that a multi-line section of text should be formatted as a code block.", false, 19),
			};

			// Add handler for the completion of the session, so we can move the cursor to a position in the inserted text, if necessary.
			session.ItemCommitted += Session_ItemCommitted;

			return new CompletionContext(completions.OrderBy(ci => ci.SortText).ToImmutableArray()); // Why doesn't VS sort?
		});

		public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) => Task.Run(() =>
		{
			if (item.Properties.TryGetProperty("description", out string description))
			{
				return description;
			}
			return (object)null;
		});

		/// <summary>
		/// Executed when autocomplete is done and has inserted a CompletionItem into the text.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Session_ItemCommitted(object sender, CompletionItemEventArgs e)
		{
			// Check if item requires the cursor to be repositioned within the CompletionItem text, e.g. in a cref attribute.
			if (e.Item.Properties.TryGetProperty("cursorRewind", out int cursorRewind))
			{
				IEditorOperations editorOperations = _operationsFactory.GetEditorOperations(_textView);
				// TODO: Is there a better way to move caret to a relative position?
				for (int i = 0; i < cursorRewind; i++)
				{
					editorOperations.MoveToPreviousCharacter(false);
				}
			}
		}

		/// <summary>
		/// Create a CompletionItem to return by GetCompletionContextAsync.
		/// </summary>
		/// <param name="addPrefix">Prefix the inserted text with a &lt; .</param>
		/// <param name="displayText">The text to display in the drop-down completion item list.</param>
		/// <param name="insertText">The text to actually insert into the document, when the CompletionItem is selected.</param>
		/// <param name="description">The description to show on MouseOver</param>
		/// <param name="isMacro">true, if it is a macro, e.g. inserted one of the langword elements. Determines the icon that is shown next to the entry in the auto-complete drop-down list.</param>
		/// <param name="cursorRewind">Optional. The number of characters to move the cursor back after auto-completion finishes, to position it within the inserted text.</param>
		/// <returns>A CompletionItem</returns>
		private CompletionItem CreateCompletionItem(bool addPrefix, string displayText, string insertText, string description, bool isMacro = false, int cursorRewind = 0)
		{
			CompletionItem returnValue = new CompletionItem(displayText, this, isMacro ? _macroIconSource : _iconSource, ImmutableArray<CompletionFilter>.Empty, string.Empty, addPrefix ? "<" + insertText : insertText, displayText, displayText, ImmutableArray<ImageElement>.Empty);
			if (!string.IsNullOrWhiteSpace(description))
			{
				returnValue.Properties.AddProperty("description", description);
			}
			if (cursorRewind > 0)
			{
				returnValue.Properties.AddProperty("cursorRewind", cursorRewind);
			}
			return returnValue;
		}
	}
}
