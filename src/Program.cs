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

            var titleC = TextBlock("<a href='https://www.powerlanguage.co.uk/wordle/' target='_blank'>Wordle</a> Assist", treatAsHTML:true).Medium().SemiBold().PR(32);

            document.body.appendChild(VStack().Background(Theme.Secondary.Background)
                                              .AlignItemsCenter().S().Children(
                                                    component.WS().H(10).Grow(), 
                                                    HStack().MB(16).AlignItemsCenter().Children(titleC, UI.GetLogo())).Render());
            document.title = title;
        }
    }
}