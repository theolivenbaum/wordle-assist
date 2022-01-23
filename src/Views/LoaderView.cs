using static H5.Core.dom;
using Tesserae;
using static Tesserae.UI;
using System.Threading.Tasks;

namespace WordleSolver
{
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
}