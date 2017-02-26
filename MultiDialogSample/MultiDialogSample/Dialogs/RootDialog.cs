using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Threading;
using AuthBot;
using AuthBot.Dialogs;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using MultiDialogSample.Models;

namespace MultiDialogSample.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        private const string FlightMenu = "Book Flight";
        private const string HotelMenu = "Book Hotel";
        private List<string> mainMenuList = new List<string>() { FlightMenu, HotelMenu };
        private string location;

        public async Task StartAsync(IDialogContext context)
        {
            await context.PostAsync("Welcome to Root Dialog");
            context.Wait(MessageReceiveAsync);
        }

        private async Task MessageReceiveAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var reply = await result;
            string accessToken = await context.GetAccessToken(ConfigurationManager.AppSettings["ActiveDirectory.ClientId"]);
            //Check login status
            if (string.IsNullOrEmpty(accessToken))
            {
                //Forward context to AzureAuthDialog for login
                await context.Forward(new AzureAuthDialog(ConfigurationManager.AppSettings["ActiveDirectory.ClientId"]), this.ResumeAfterAuth, reply, CancellationToken.None);
            }
            else
            {
                await ShowMainmenu(context);
            }
        }

        private async Task ResumeAfterAuth(IDialogContext context, IAwaitable<string> result)
        {
            var message = await result;
            await context.PostAsync(message);
            await ShowMainmenu(context);
        }

        private async Task ShowMainmenu(IDialogContext context)
        {
            //Get access token from Bot State Service
            string token = await context.GetAccessToken(ConfigurationManager.AppSettings["ActiveDirectory.ResourceId"]);
            string firstName = await GetDisplayName(token);

            //Show menues
            PromptDialog.Choice(context, this.CallDialog, this.mainMenuList, $"What do you want to do, {firstName}?");
        }

        private async Task<string> GetDisplayName(string token)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                //Set Header Value
                var acceptHeader = new MediaTypeWithQualityHeaderValue("application/json");
                httpClient.DefaultRequestHeaders.Accept.Add(acceptHeader);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage response = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/me/?$select=displayName");
                UserModel user = await response.Content.ReadAsAsync<UserModel>();
                return user.displayName;
            }
        }

        private async Task CallDialog(IDialogContext context, IAwaitable<string> result)
        {
            //This method is resume after user choise menu
            var selectedMenu = await result;
            switch (selectedMenu)
            {
                case FlightMenu:
                    //Call child dialog without data
                    context.Call(new FlightDialog(), ResumeAfterDialog);
                    break;
                case HotelMenu:
                    //Call child dialog with data
                    context.Call(new HotelDialog(location), ResumeAfterDialog);
                    break;
            }
        }

        private async Task ResumeAfterDialog(IDialogContext context, IAwaitable<object> result)
        {
            //Resume this method after child Dialog is done.
            var test = await result;
            if (test != null)
            {
                location = test.ToString();
            }
            else
            {
                location = null;
            }
            //await this.ShowMainmenu(context); // If you want to show main menu when the dialog is done, please comment out this line.
            context.Wait(MessageReceiveAsync);
        }
    }
}