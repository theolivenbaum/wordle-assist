using System;
using H5.Core;
using static H5.Core.es5;
using static H5.Core.dom;
using Tesserae;
using static Tesserae.UI;
using System.Threading.Tasks;
using TNT;
using static TNT.T;

namespace WordleSolver
{
    class Program
    {
        static void Main(string[] args)
        {
            Show(new LoaderView(), "Wordle Solver");
        }

        public static void Show(IComponent component, string title)
        {
            ClearChildren(document.body);
            document.body.appendChild(VStack().Background(Theme.Secondary.Background).AlignItemsCenter().S().Children(component.WS().H(10).Grow(), UI.GetLogo()).Render());
            document.title = title;
        }
    }

    public static class UI
    {
        public static BackgroundArea CenteredCardWithBackground(IComponent content)
        {
            var card = Card(content).NoAnimation().Padding(32.px());
            card.Render().style.maxHeight = "calc(100% - 32px)";
            return BackgroundArea(card).S();
        }

        public static BackgroundArea CenteredWithBackground(IComponent content) => BackgroundArea(content).S();

        public static IComponent GetLogo()
        {
            return Link("https://curiosity.ai", HStack().AlignItemsCenter().Children(TextBlock("by Curiosity").Primary().PR(8), Image("./assets/img/icon.png").H(32).W(32).Contain())).Class("floating-logo")
                    .Tooltip(TextBlock("One search, all your apps and files. <br/>Get Curiosity for free!", treatAsHTML: true).TextCenter());
        }
    }
}