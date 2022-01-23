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
        private const string ABSENT = "state-absent";
        private const string PRESENT = "state-present";
        private const string CORRECT = "state-correct";
        private const string EMPTY = "state-empty";
        private const string TBD = "state-tbd";
        private const string KEY_ENTER = "Enter";
        private const string KEY_BACKSPACE = "Backspace";

        private readonly IComponent _explainer;
        private readonly TextBlock[][] _board;
        private readonly Stack _suggestions;
        private readonly Grid[] _rows;
        private readonly IComponent _container;
        private int _currentRow;
        private int _currentLetter;
        private readonly HashSet<char> _validKeys = new HashSet<char>("abcdefghijklmnopqrstuvwxyz");

        private static readonly string[] _keys = new[] { "qwertyuiop", "asdfghjkl", ">yxcvbnm<" };


        private CancellationTokenSource _cancelCompute = new CancellationTokenSource();  

        public SolverView()
        {
            _explainer = Stack().WS().Children(TextBlock("Type your guess, press enter to accept, and click on the letters to toggle their color").TextCenter().Secondary());
            
            _board = new TextBlock[6][];
            
            _currentRow = 0;
            _currentLetter = 0;

            for (int row = 0; row < 6; row++)
            {
                var letters = new TextBlock[5];
            
                letters[0] = TextBlock().Class("tile").Class(EMPTY);
                letters[1] = TextBlock().Class("tile").Class(EMPTY);
                letters[2] = TextBlock().Class("tile").Class(EMPTY);
                letters[3] = TextBlock().Class("tile").Class(EMPTY);
                letters[4] = TextBlock().Class("tile").Class(EMPTY);
                
                _board[row] = letters;
            }

            _suggestions = VStack().MinWidth(128.px()).MaxWidth(420.px()).PL(32).PB(6).HS();

            _rows = _board.Select((b, i) => Grid().Class("row").W(500).WS().Children(b).MT(i == 0 ? 0 : -3)).ToArray();

            var boardContainer = Stack().Children(_rows);

            var gameArea = Stack().AlignItemsCenter().S()
                                  .Children(boardContainer,
                                            HStack().W(420).HS().Children(Empty().Grow(), _suggestions.HS(), Empty().Grow()));

            if (window.outerWidth > 500)
            {
                gameArea.Horizontal();
                boardContainer.W(250);
            }
            else
            {
                boardContainer.H(500).W(420);
                _suggestions.H(420);
            }

            _container = UI.CenteredWithBackground(VStack().AlignItemsCenter().Children(gameArea, _explainer.PT(32).PB(32), BuildKeyboard()));

            HookOnClick();
            HookKeyboardHandle();
            RefreshCandidates().FireAndForget();
        }

        private Stack BuildKeyboard()
        {
            return VStack().WS().AlignItemsCenter()
                           .Children(_keys.Select(line => HStack().NoWrap().WS().AlignItemsCenter().JustifyContent(ItemJustify.Center)
                                                                  .Children(line.Select(l => GetKey(l)))));
            
            IComponent GetKey(char l)
            {
                var key = l.ToString();
                if (l == '<') key = KEY_BACKSPACE; 
                else if (l == '>') key = KEY_ENTER;
                var btn = Button(key).Class("keyboard-key").OnClick(() => HandleKey(key));
                if (l == '<') btn.W(96);
                if (l == '>') btn.W(64);
                return btn;
            }
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
                    if (tile.classList.contains(TBD))
                    {
                        Tippy.ShowFor(component, TextBlock("Type your word and press enter first"), out var onHide, TooltipAnimation.ShiftAway, TooltipPlacement.Bottom);
                        window.setTimeout((_) => onHide(), 500);
                        return;
                    }
                    else if (tile.classList.contains(ABSENT))
                    {
                        tile.classList.remove(ABSENT);
                        tile.classList.add(PRESENT);
                    }
                    else if (tile.classList.contains(PRESENT))
                    {
                        tile.classList.remove(PRESENT);
                        tile.classList.add(CORRECT);
                    }
                    else if (tile.classList.contains(CORRECT))
                    {
                        tile.classList.remove(CORRECT);
                        tile.classList.add(ABSENT);
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
            var checkedLetters = new HashSet<char>();
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
                            case State.Correct:
                            {
                                greenLettersArray[col] = letter; 
                                greenLetters.Add(letter);
                                remainingCandidates.RemoveWhere(c => c[col] != letter);
                                break;
                            }
                            case State.Present:
                            {
                                if (checkedLetters.Add(letter))
                                {
                                    remainingCandidates.RemoveWhere(c => !c.Contains(letter)); 
                                }
                                break;
                            }
                            case State.Absent:
                            {
                                if (!greenLetters.Contains(letter)) 
                                { 
                                    remainingCandidates.RemoveWhere(c => c.Contains(letter));
                                }
                                break;
                            }
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
                Empty().Grow(),
                VStack().WS().Children(
                    TextBlock("Most Greens").SemiBold().Secondary(),
                    HStack().Wrap().WS().Children(mostGreens.Select(w => RenderWord(w.Word).PR(12)))),
                Empty().Grow(),
                VStack().WS().Children(
                    TextBlock("Most Yellows").SemiBold().Secondary(),
                    HStack().Wrap().WS().Children(mostYellows.Select(w => RenderWord(w.Word).PR(12)))),
                Empty().Grow(),
                VStack().WS().Children(
                    TextBlock("Most Vowels").SemiBold().Secondary(),
                    HStack().Wrap().WS().Children(mostVowels.Select(w => RenderWord(w.Word).PR(12)))),
                Empty().Grow(),
                VStack().WS().Children(
                    TextBlock("Most Common").SemiBold().Secondary(),
                    HStack().Wrap().WS().Children(mostCommon.Select(w => RenderWord(w.Word).PR(12)))),
                Empty().Grow()
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
                if (tile.classList.contains(ABSENT))
                {
                    return State.Absent;
                }
                else if (tile.classList.contains(PRESENT))
                {
                    return State.Present;
                }
                else if (tile.classList.contains(CORRECT))
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
                if (e.ctrlKey || e.altKey || e.metaKey) return;
                HandleKey(e.key);
                StopEvent(e);
            };
        }

        private void HandleKey(string keyPressed)
        {
            if (keyPressed.Length == 1 && _validKeys.Contains(char.ToLower(keyPressed[0])))
            {
                if (_currentLetter < 5)
                {
                    if (string.IsNullOrWhiteSpace(_board[_currentRow][_currentLetter].Text))
                    {
                        _board[_currentRow][_currentLetter].Text = keyPressed.ToLower();
                        _board[_currentRow][_currentLetter].RemoveClass(EMPTY).Class(TBD);
                        if (_currentLetter < 4) _currentLetter++;
                    }
                }
            }
            else
            {
                if (keyPressed == KEY_ENTER)
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
                                if (tile.classList.contains(ABSENT) || tile.classList.contains(PRESENT) || tile.classList.contains(CORRECT)) continue;

                                var finalState = State.Absent;
                                if (_currentRow > 0)
                                {
                                    for (int row = 0; row < _currentRow; row++)
                                    {
                                        if (state.States[row][col] == State.Correct && state.Letters[row][col] == word[col])
                                        {
                                            finalState = State.Correct;
                                            break;
                                        }
                                    }
                                }

                                tile.classList.add(finalState == State.Absent ? ABSENT : CORRECT);
                                tile.classList.remove(TBD);
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
                else if (keyPressed == KEY_BACKSPACE)
                {
                    if (_currentLetter == 0)
                    {
                        if (_currentRow > 0)
                        {
                            if (string.IsNullOrEmpty(_board[_currentRow][_currentLetter].Text))
                            {
                                _currentRow--;
                                _currentLetter = 4;
                                
                                var row = _board[_currentRow];

                                row[_currentLetter].Text = "";
                                row[_currentLetter].RemoveClass(ABSENT).RemoveClass(TBD).RemoveClass(PRESENT).RemoveClass(CORRECT).Class(EMPTY);

                                for (int col = 0; col < _currentLetter; col++)
                                {
                                    row[col].RemoveClass(ABSENT).RemoveClass(EMPTY).RemoveClass(PRESENT).RemoveClass(CORRECT).Class(TBD);
                                }

                                RefreshCandidates().FireAndForget();
                            }
                            else
                            {
                                var row = _board[_currentRow];

                                row[_currentLetter].Text = "";
                                row[_currentLetter].RemoveClass(ABSENT).RemoveClass(TBD).RemoveClass(PRESENT).RemoveClass(CORRECT).Class(EMPTY);
                                for (int col = 0; col < _currentLetter - 1; col++)
                                {
                                    row[col].RemoveClass(ABSENT).RemoveClass(EMPTY).RemoveClass(PRESENT).RemoveClass(CORRECT).Class(TBD);
                                }
                            }
                        }
                        else
                        {
                            _board[0][0].Text = "";
                            _board[0][0].RemoveClass(ABSENT).RemoveClass(TBD).RemoveClass(PRESENT).RemoveClass(CORRECT).Class(EMPTY);
                            for (int col = 0; col < _currentLetter; col++)
                            {
                                _board[_currentRow][col].RemoveClass(ABSENT).RemoveClass(TBD).RemoveClass(PRESENT).RemoveClass(CORRECT).Class(TBD);
                            }
                        }
                    }
                    else
                    {
                        var row = _board[_currentRow];
                        if (_currentLetter > 0 && string.IsNullOrEmpty(row[_currentLetter].Text))
                        {
                            _currentLetter--;
                        }
                        
                        row[_currentLetter].Text = "";
                        row[_currentLetter].RemoveClass(ABSENT).RemoveClass(TBD).RemoveClass(PRESENT).RemoveClass(CORRECT).Class(EMPTY);

                        for (int col = 0; col < _currentLetter; col++)
                        {
                            row[col].RemoveClass(ABSENT).RemoveClass(EMPTY).RemoveClass(PRESENT).RemoveClass(CORRECT).Class(TBD);
                        }
                    }
                }
            }
        }

        public HTMLElement Render() => _container.Render();
    }
}