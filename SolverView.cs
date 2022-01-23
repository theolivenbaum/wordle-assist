using static H5.Core.dom;
using Tesserae;
using static Tesserae.UI;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace WordleSolver
{
    public class SolverView : IComponent
    {
        private readonly IComponent _explainer;
        private readonly Stack _title;
        private readonly TextBlock[][] _board;
        private readonly Stack _suggestions;
        private readonly Stack _boardContainer;
        private readonly Stack _keyboard;
        private readonly Grid[] _rows;
        private readonly IComponent _container;
        private int _currentRow;
        private int _currentLetter;
        private readonly HashSet<char> _validKeys = new HashSet<char>("abcdefghijklmnopqrstuvwxyz");

        private CancellationTokenSource _cancelCompute = new CancellationTokenSource();  

        public SolverView()
        {
            _explainer = Stack().WS().Children(TextBlock("Type your guess, press enter to accept, and click on the letters to toggle their color").TextCenter().Secondary().PT(32));
            _title     = Stack().WS().Children(TextBlock("Wordle Assist").Medium().SemiBold().PB(32).PL(6));
            
            _board = new TextBlock[6][];
            _currentRow = 0;
            _currentLetter = 0;

            for (int row = 0; row < 6; row++)
            {
                var letters = new TextBlock[5];
                letters[0] = TextBlock().Class("tile").Class("state-empty");
                letters[1] = TextBlock().Class("tile").Class("state-empty");
                letters[2] = TextBlock().Class("tile").Class("state-empty");
                letters[3] = TextBlock().Class("tile").Class("state-empty");
                letters[4] = TextBlock().Class("tile").Class("state-empty");
                _board[row] = letters;
            }

            _suggestions = VStack().H(420).MinWidth(128.px()).MaxWidth(420.px()).PL(32).PB(6);

            _rows = _board.Select((b, i) => Grid().Class("row").W(600).WS().Children(b).MT(i == 0 ? 0 : -3)).ToArray();

            _boardContainer = Stack().H(420).W(350).Children(_rows);

            _keyboard = Stack().WS();
            
            _container = UI.CenteredWithBackground(
                VStack().AlignItemsCenter().Children(
                    _title,
                    HStack().Children(
                        _boardContainer,
                        HStack().W(420).Children(
                            Empty().Grow(), _suggestions.NoShrink(), Empty().Grow())),
                    _explainer, _keyboard));

            HookOnClick();
            HookKeyboardHandle();
            RefreshCandidates().FireAndForget();
        }

        private void HookOnClick()
        {
            foreach(var row in _board)
            {
                foreach(var tileComponent in row)
                {
                    var tile = tileComponent.Render();
                    tile.onclick = (e) => NextState(tileComponent, tile);  ;
                }
            }
            
            void NextState(IComponent component, HTMLElement tile)
            {
                if (!string.IsNullOrEmpty(tile.textContent))
                {
                    if (tile.classList.contains("state-tbd"))
                    {
                        Tippy.ShowFor(component, TextBlock("Type your word and press enter first"), out var onHide, TooltipAnimation.ShiftAway, TooltipPlacement.Bottom);
                        window.setTimeout((_) => onHide(), 500);
                        return;
                    }
                    else if (tile.classList.contains("state-absent"))
                    {
                        tile.classList.remove("state-absent");
                        tile.classList.add("state-present");
                    }
                    else if (tile.classList.contains("state-present"))
                    {
                        tile.classList.remove("state-present");
                        tile.classList.add("state-correct");
                    }
                    else if (tile.classList.contains("state-correct"))
                    {
                        tile.classList.remove("state-correct");
                        tile.classList.add("state-absent");
                    }

                    RefreshCandidates().FireAndForget();
                }
            }
        }

        private async Task RefreshCandidates()
        {
            _cancelCompute.Cancel();
            _cancelCompute = new CancellationTokenSource();
            var token = _cancelCompute.Token;

            var progress = ProgressIndicator().W(128);

            _suggestions.Children(VStack().S().Children(Empty().Grow(),progress, Empty().Grow()));

            await Task.Delay(100, token);

            var remainingCandidates = new HashSet<string>(Words.Answers);

            var board = ReadBoard();

            var greenLetters = new HashSet<char>();
            var greenLettersArray = new char[5];

            if (board.States[0].Any(s => s != State.TBD))
            {
                for (int row = 0; row < 6; row++)
                {
                    var states = board.States[row];
                    var letters = board.Letters[row];
                    if (states.Any(s => s == State.TBD)) continue;

                    for (int col = 0; col < 5; col++)
                    {
                        progress.Progress(col + row * 5, 5 * 6);

                        await Task.Delay(0, token);

                        var letter = letters[col];

                        switch (states[col])
                        {
                            case State.Correct: greenLettersArray[col] = letter; greenLetters.Add(letter); remainingCandidates.RemoveWhere(c => c[col] != letter); break;
                            case State.Present: remainingCandidates.RemoveWhere(c => !c.Contains(letter)); break;
                            case State.Absent: if (!greenLetters.Contains(letter)) { remainingCandidates.RemoveWhere(c => c.Contains(letter)); } break;
                        }
                    }
                }

                if (remainingCandidates.Count == 0)
                {
                    _suggestions.Children(VStack().S().Children(Empty().Grow(), TextBlock("No valid answers left, maybe you set a letter to the wrong color?").TextCenter(), Empty().Grow()));
                }
                else
                {
                    var scores = await Words.GetNextScores(remainingCandidates, progress, token);
                    RenderSuggestions(scores, greenLettersArray);
                }
            }
            else
            {
                RenderSuggestions(Words.InitialScores, greenLettersArray);
            }
        }

        private void RenderSuggestions(WordStat[] scores, char[] greenChars)
        {
            var mostGreens  = scores.OrderByDescending(s => s.Greens).Take(6).ToArray();
            var mostYellows = scores.OrderByDescending(s => s.Yellows).Take(6).ToArray();
            var mostVowels  = scores.OrderByDescending(s => s.Vowels).Take(6).ToArray();
            var mostCommon  = scores.OrderByDescending(s => s.Common).Take(6).ToArray();

            _suggestions.Children(
                Empty().H(10).Grow(),
                VStack().WS().Children(
                    TextBlock("Most Greens").SemiBold().Secondary(),
                    HStack().Wrap().WS().Children(mostGreens.Select(w => RenderWord(w.Word).PR(12)))),
                Empty().H(10).Grow(),
                VStack().WS().Children(
                    TextBlock("Most Yellows").SemiBold().Secondary(),
                    HStack().Wrap().WS().Children(mostYellows.Select(w => RenderWord(w.Word).PR(12)))),
                Empty().H(10).Grow(),
                VStack().WS().Children(
                    TextBlock("Most Vowels").SemiBold().Secondary(),
                    HStack().Wrap().WS().Children(mostVowels.Select(w => RenderWord(w.Word).PR(12)))),
                Empty().H(10).Grow(),
                VStack().WS().Children(
                    TextBlock("Most Common").SemiBold().Secondary(),
                    HStack().Wrap().WS().Children(mostCommon.Select(w => RenderWord(w.Word).PR(12)))),
                Empty().H(10).Grow()
            );

            IComponent RenderWord(string word)
            {
                var div = Div(_("tiny-row"), word.Select((c,i) => Span(_("tiny-tile" + (greenChars[i] == c ? " state-correct" : ""), text:c.ToString()))).ToArray());
                return Raw(div);
            }
        }

        private BoardState ReadBoard()
        {
            return new BoardState()
            {
                States = _board.Select(row => row.Select(tile => GetState(tile.Render())).ToArray()).ToArray(),
                Letters = _board.Select(row => row.Select(tile => GetLetter(tile)).ToArray()).ToArray(),
            };

            char GetLetter(TextBlock tile)
            {
                if (string.IsNullOrEmpty(tile.Text)) return ' ';
                else return char.ToLower(tile.Text[0]);
            }

            State GetState(HTMLElement tile)
            {
                if (tile.classList.contains("state-absent"))
                {
                    return State.Absent;
                }
                else if (tile.classList.contains("state-present"))
                {
                    return State.Present;
                }
                else if (tile.classList.contains("state-correct"))
                {
                    return State.Correct;
                }
                return State.TBD;
            }
        }

        private void HookKeyboardHandle()
        {
            window.onkeydown += (e) =>
            {
                if (e.ctrlKey) return;

                //console.log($"Before: R:{_currentRow} L:{_currentLetter}");

                if (e.key.Length == 1 && _validKeys.Contains(char.ToLower(e.key[0])))
                {
                    if (_currentLetter < 5)
                    {
                        if (string.IsNullOrWhiteSpace(_board[_currentRow][_currentLetter].Text))
                        {
                            _board[_currentRow][_currentLetter].Text = e.key.ToLower();
                            _board[_currentRow][_currentLetter].RemoveClass("state-empty").Class("state-tbd");
                            if (_currentLetter < 4) _currentLetter++;
                        }
                    }
                }
                else
                {
                    if (e.key == "Enter")
                    {
                        if (_currentLetter == 4 && _currentRow < 5)
                        {
                            var state = ReadBoard();
                            var word = new string(state.Letters[_currentRow]);

                            if (Words.ValidWords.Contains(word))
                            {
                                for (int col = 0; col < _board[_currentRow].Length; col++)
                                {
                                    var tileComponent = _board[_currentRow][col];
                                    var tile = tileComponent.Render();
                                    if (tile.classList.contains("state-absent") || tile.classList.contains("state-present") || tile.classList.contains("state-correct")) continue;

                                    var finalState = State.Absent;
                                    if(_currentRow > 0)
                                    {
                                        for(int row = 0; row < _currentRow; row++)
                                        {
                                            if(state.States[row][col] == State.Correct && state.Letters[row][col] == word[col])
                                            {
                                                finalState = State.Correct;
                                                break;
                                            }
                                        }
                                    }
                                    
                                    tile.classList.add(finalState == State.Absent ? "state-absent" : "state-correct");
                                    tile.classList.remove("state-tbd");
                                }
                                _currentRow++;
                                _currentLetter = 0;
                                RefreshCandidates().FireAndForget();
                            }
                            else
                            {
                                _rows[_currentRow].Class("invalid");
                                window.setTimeout((_) => _rows[_currentRow].RemoveClass("invalid"), 600);
                            }
                        }
                    }
                    else if(e.key == "Backspace")
                    {
                        if (_currentLetter == 0)
                        {
                            if (_currentRow > 0)
                            {
                                if (string.IsNullOrEmpty(_board[_currentRow][_currentLetter].Text))
                                {
                                    _currentRow--;
                                    _currentLetter = 4;
                                    _board[_currentRow][_currentLetter].Text = "";
                                    _board[_currentRow][_currentLetter].RemoveClass("state-absent").RemoveClass("state-tbd").RemoveClass("state-present").RemoveClass("state-correct").Class("state-empty");
                                    for(int col = 0; col < _currentLetter; col++)
                                    {
                                        _board[_currentRow][col].RemoveClass("state-absent").RemoveClass("state-empty").RemoveClass("state-present").RemoveClass("state-correct").Class("state-tbd");
                                    }
                                    RefreshCandidates().FireAndForget();
                                }
                                else
                                {
                                    _board[_currentRow][_currentLetter].Text = "";
                                    _board[_currentRow][_currentLetter].RemoveClass("state-absent").RemoveClass("state-tbd").RemoveClass("state-present").RemoveClass("state-correct").Class("state-empty");
                                    for (int col = 0; col < _currentLetter - 1; col++)
                                    {
                                        _board[_currentRow][col].RemoveClass("state-absent").RemoveClass("state-empty").RemoveClass("state-present").RemoveClass("state-correct").Class("state-tbd");
                                    }
                                }
                            }
                            else
                            {
                                _board[0][0].Text = "";
                                _board[0][0].RemoveClass("state-absent").RemoveClass("state-tbd").RemoveClass("state-present").RemoveClass("state-correct").Class("state-empty");
                                for (int col = 0; col < _currentLetter; col++)
                                {
                                    _board[_currentRow][col].RemoveClass("state-absent").RemoveClass("state-tbd").RemoveClass("state-present").RemoveClass("state-correct").Class("state-tbd");
                                }
                            }
                        }
                        else
                        {
                            if(_currentLetter > 0 && string.IsNullOrEmpty(_board[_currentRow][_currentLetter].Text))
                            {
                                _currentLetter--;
                            }
                            _board[_currentRow][_currentLetter].Text = "";
                            _board[_currentRow][_currentLetter].RemoveClass("state-absent").RemoveClass("state-tbd").RemoveClass("state-present").RemoveClass("state-correct").Class("state-empty");
                            for (int col = 0; col < _currentLetter; col++)
                            {
                                _board[_currentRow][col].RemoveClass("state-absent").RemoveClass("state-empty").RemoveClass("state-present").RemoveClass("state-correct").Class("state-tbd");
                            }
                        }
                    }
                }

                StopEvent(e);
                //console.log($"After  R:{_currentRow} L:{_currentLetter}");
            };
        }

        public HTMLElement Render() => _container.Render();
    }
    
    public class LoaderView: IComponent
    {
        private readonly IComponent _container;
        
        public LoaderView()
        {
            var pi = ProgressIndicator();
            _container = UI.CenteredWithBackground(Stack().W(50.vw()).Children(TextBlock("Loading words list...").SemiBold().TextCenter().PB(32), pi));
            LoadAsync(pi).FireAndForget();
        }

        private async Task LoadAsync(ProgressIndicator pi)
        {
            await Words.PreloadInitialScores(pi);
            Program.Show(new SolverView(), "Wordle Solver");
        }

        private static void HookKeyboardHandle()
        {
            window.onkeydown += (e) =>
            {
                StopEvent(e);
            };
        }

        public HTMLElement Render() => _container.Render();
    }

    public class BoardState
    {
        public State[][] States  { get; set; }
        public char[][]  Letters { get; set; }
    }

    public enum State
    {
        TBD,
        Present,
        Correct,
        Absent
    }
}