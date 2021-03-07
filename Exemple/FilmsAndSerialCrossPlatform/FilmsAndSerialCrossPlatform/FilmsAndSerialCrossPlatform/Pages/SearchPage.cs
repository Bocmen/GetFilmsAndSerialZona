using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Xamarin.Forms;

namespace FilmsAndSerialCrossPlatform.Pages
{
    public class SearchPage : ContentPage
    {

        public SearchPage()
        {
            Button buttonT = new Button { Text = "NewPage" };
            buttonT.Clicked += ButtonT_Clicked;
            Content = new StackLayout
            {
                Children = {
                    new Label { Text = "Welcome to Xamarin.Forms!" },
                    buttonT
                }
            };
        }

        private void ButtonT_Clicked(object sender, EventArgs e)
        {
            Navigation.PushAsync(new SearchPage());
        }
    }
}