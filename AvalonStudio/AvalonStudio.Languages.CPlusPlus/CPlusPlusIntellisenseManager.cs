﻿namespace AvalonStudio.Languages.CPlusPlus
{
    using Languages;
    using Perspex.Input;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using TextEditor.Document;
    using Utils;
    using ViewModels;

    class CPlusPlusIntellisenseManager
    {
        public CPlusPlusIntellisenseManager (IIntellisenseControl intellisenseControl, TextEditor.TextEditor editor)
        {
            this.intellisenseControl = intellisenseControl;
            this.editor = editor;
        }

        private TextEditor.TextEditor editor;
        private IIntellisenseControl intellisenseControl;
        private string currentFilter = string.Empty;
        private int intellisenseStartedAt;
        private object intellisenseLock = new object();

        private bool IsIntellisenseOpenKey(KeyEventArgs e)
        {
            bool result = false;

            result = (e.Key >= Key.D0 && e.Key <= Key.D9 && e.Modifiers == InputModifiers.None) || (e.Key >= Key.A && e.Key <= Key.Z) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) || (e.Key == Key.Oem1);

            return result;
        }

        private bool IsIntellisenseFilterModificationKey(KeyEventArgs e)
        {
            bool result = false;

            result = IsIntellisenseOpenKey(e);

            if (!result)
            {
                switch (e.Key)
                {
                    case Key.Back:
                    case Key.OemPeriod:
                    case Key.Oem1:
                        result = true;
                        break;
                }
            }

            if (!result && e.Modifiers == InputModifiers.Shift)
            {
                switch (e.Key)
                {
                    case Key.OemMinus:
                        result = true;
                        break;
                }
            }

            return result;
        }

        private bool IsCompletionKey(KeyEventArgs e)
        {
            bool result = false;

            if (e.Modifiers == InputModifiers.None)
            {
                switch (e.Key)
                {
                    case Key.Enter:
                    case Key.Tab:
                    case Key.OemPeriod:
                    case Key.OemMinus:
                    case Key.Space:
                        result = true;
                        break;
                }
            }

            return result;
        }

        private bool IsAllowedNonFilterModificationKey(KeyEventArgs e)
        {
            bool result = false;

            if (e.Key >= Key.LeftShift && e.Key <= Key.RightShift)
            {
                result = true;
            }

            if (!result)
            {
                switch (e.Key)
                {
                    case Key.Up:
                    case Key.Down:
                    case Key.None:
                        result = true;
                        break;
                }
            }

            return result;
        }

        private bool IsIntellisenseKey(KeyEventArgs e)
        {
            return IsIntellisenseFilterModificationKey(e);
        }

        private bool IsIntellisenseResetKey(KeyEventArgs e)
        {
            bool result = false;

            if (e.Key == Key.OemPeriod)
            {
                result = true;
            }

            return result;
        }

        

        public void OnKeyDown(KeyEventArgs e)
        {
            CompleteOnKeyDown(e);
        }

        private CompletionDataViewModel noSelectedCompletion = new CompletionDataViewModel(null);

        private List<CompletionDataViewModel> unfilteredCompletions = new List<CompletionDataViewModel>();

        private bool DoComplete(bool includeLastChar)
        {
            bool result = false;

            if (intellisenseControl.CompletionData.Count > 0 && intellisenseControl.SelectedCompletion != noSelectedCompletion)
            {
                int offset = 0;

                if (includeLastChar)
                {
                    offset = 1;
                }

                editor.TextDocument.Replace(intellisenseStartedAt, editor.CaretIndex - intellisenseStartedAt - offset, intellisenseControl.SelectedCompletion.Title);
                editor.CaretIndex = intellisenseStartedAt + intellisenseControl.SelectedCompletion.Title.Length + offset;

                result = true;
            }

            return result;
        }

        private void CompleteOnKeyDown(KeyEventArgs e)
        {
            if (intellisenseControl.IsVisible && e.Modifiers == InputModifiers.None)
            {
                if (IsCompletionKey(e))
                {
                    e.Handled = DoComplete(false);
                }
                else
                {
                    switch (e.Key)
                    {
                        case Key.Down:
                            {
                                int index = intellisenseControl.CompletionData.IndexOf(intellisenseControl.SelectedCompletion);

                                if (index < intellisenseControl.CompletionData.Count - 1)
                                {
                                    intellisenseControl.SelectedCompletion = intellisenseControl.CompletionData[index + 1];
                                }

                                e.Handled = true;
                            }
                            break;

                        case Key.Up:
                            {
                                int index = intellisenseControl.CompletionData.IndexOf(intellisenseControl.SelectedCompletion);

                                if (index > 0)
                                {
                                    intellisenseControl.SelectedCompletion = intellisenseControl.CompletionData[index - 1];
                                }

                                e.Handled = true;
                            }
                            break;
                    }
                }
            }
        }

        private void CompleteOnKeyUp()
        {
            if (intellisenseControl.IsVisible)
            {
                char behindCaretChar = '\0';
                char behindBehindCaretChar = '\0';

                if (editor.CaretIndex > 0)
                {
                    behindCaretChar = editor.TextDocument.GetCharAt(editor.CaretIndex - 1);
                }

                if (editor.CaretIndex > 1)
                {
                    behindBehindCaretChar = editor.TextDocument.GetCharAt(editor.CaretIndex - 2);
                }

                switch (behindCaretChar)
                {
                    case '(':
                    case '=':
                    case '+':
                    case '-':
                    case '*':
                    case '/':
                    case '%':
                    case '|':
                    case '&':
                    case '!':
                    case '^':
                    case ' ':
                    case ':':
                    case '.':
                        DoComplete(true);
                        return;
                }
            }
        }

        private void Close()
        {
            intellisenseControl.SelectedCompletion = noSelectedCompletion;
            intellisenseStartedAt = editor.CaretIndex;
            intellisenseControl.IsVisible = false;
            currentFilter = string.Empty;
        }

        private TextLocation CaretTextLocation
        {
            get
            {
                return editor.TextDocument.GetLocation(editor.CaretIndex);
            }
        }

        public async void OnKeyUp(KeyEventArgs e)
        {
            bool isVisible = intellisenseControl.IsVisible;

            if (IsIntellisenseKey(e))
            {
                var caretIndex = editor.CaretIndex;

                if (caretIndex <= intellisenseStartedAt)
                {
                    Close();
                    return;
                }

                if (IsIntellisenseResetKey(e))
                {
                    isVisible = false;  // We dont actually want to hide, so use backing field.
                    //currentFilter = string.Empty;
                }

                CompleteOnKeyUp();

                IEnumerable<CompletionDataViewModel> filteredResults = null;

                if (!intellisenseControl.IsVisible && (IsIntellisenseOpenKey(e) || IsIntellisenseResetKey(e)))
                {
                    var caret = CaretTextLocation;

                    char behindCaretChar = '\0';
                    char behindBehindCaretChar = '\0';

                    if (editor.CaretIndex > 0)
                    {
                        behindCaretChar = editor.TextDocument.GetCharAt(editor.CaretIndex - 1);
                    }

                    if (editor.CaretIndex > 1)
                    {
                        behindBehindCaretChar = editor.TextDocument.GetCharAt(editor.CaretIndex - 2);
                    }

                    if (behindCaretChar == ':' && behindBehindCaretChar == ':')
                    {
                        intellisenseStartedAt = caretIndex;
                    }
                    else if (behindCaretChar == '>' || behindBehindCaretChar == ':')
                    {
                        intellisenseStartedAt = caretIndex - 1;
                    }
                    else
                    {
                        intellisenseStartedAt = TextUtilities.GetNextCaretPosition(editor.TextDocument, caretIndex, TextUtilities.LogicalDirection.Backward, TextUtilities.CaretPositioningMode.WordStart);
                    }

                    if (IsIntellisenseResetKey(e))
                    {
                        intellisenseStartedAt++;
                    }

                    currentFilter = editor.TextDocument.GetText(intellisenseStartedAt, caretIndex - intellisenseStartedAt);

                    var codeCompletionResults = await intellisenseControl.DoCompletionRequestAsync(caret.Line, caret.Column, currentFilter);

                    await Task.Factory.StartNew(() =>
                    {
                        lock (intellisenseLock)
                        {
                            unfilteredCompletions.Clear();

                            if (codeCompletionResults != null)
                            {
                                foreach (var result in codeCompletionResults.Completions)
                                {
                                    if (result.Suggestion.ToLower().Contains(currentFilter.ToLower()))
                                    {
                                        CompletionDataViewModel currentCompletion = null;

                                        currentCompletion = unfilteredCompletions.BinarySearch(c => c.Title, result.Suggestion);

                                        if (currentCompletion == null)
                                        {
                                            unfilteredCompletions.Add(CompletionDataViewModel.Create(result));
                                        }
                                        else
                                        {
                                            //currentCompletion.NumOverloads++;
                                        }
                                    }
                                }
                            }
                        }
                    });

                    filteredResults = unfilteredCompletions.ToList();
                }
                else
                {
                    if (intellisenseStartedAt != -1)
                    {
                        currentFilter = editor.TextDocument.GetText(intellisenseStartedAt, caretIndex - intellisenseStartedAt);
                    }
                    else
                    {
                        currentFilter = string.Empty;
                    }

                    await Task.Factory.StartNew(() =>
                    {
                        lock (intellisenseLock)
                        {
                            filteredResults = unfilteredCompletions.Where((c) => c.Title.ToLower().Contains(currentFilter.ToLower())).ToList();
                        }
                    });
                }

                CompletionDataViewModel suggestion = null;
                if (currentFilter != string.Empty)
                {
                    IEnumerable<CompletionDataViewModel> newSelectedCompletions = null;

                    lock (intellisenseLock)
                    {
                        newSelectedCompletions = filteredResults.Where((s) => s.Title.StartsWith(currentFilter));   // try find exact match case sensitive

                        if (newSelectedCompletions.Count() == 0)
                        {
                            newSelectedCompletions = filteredResults.Where((s) => s.Title.ToLower().StartsWith(currentFilter.ToLower()));   // try find non-case sensitve match
                        }

                        filteredResults = newSelectedCompletions;
                    }

                    if (newSelectedCompletions.Count() == 0)
                    {
                        suggestion = noSelectedCompletion;
                    }
                    else
                    {
                        var newSelectedCompletion = newSelectedCompletions.FirstOrDefault();

                        suggestion = newSelectedCompletion;
                    }
                }
                else
                {
                    suggestion = noSelectedCompletion;
                }

                if (filteredResults?.Count() > 0)
                {
                    if (filteredResults?.Count() == 1 && filteredResults.First().Title == currentFilter)
                    {
                        Close();
                    }
                    else
                    {
                        var list = filteredResults.ToList();

                        intellisenseControl.CompletionData = list.Skip(list.IndexOf(suggestion) - 25).Take(50).ToList();
                        //Model = filteredResults.ToList();                   

                        intellisenseControl.SelectedCompletion = suggestion;

                        // Triggers display update.
                        intellisenseControl.IsVisible = true;
                    }
                }
                else
                {
                    Close();
                }
            }
            else if (IsAllowedNonFilterModificationKey(e))
            {
                // do nothing
            }
            else
            {
                if (intellisenseControl.IsVisible && IsCompletionKey(e))
                {
                    e.Handled = true;
                }

                Close();
            }
        }
    }
}