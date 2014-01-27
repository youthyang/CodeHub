﻿// --------------------------------------------------------------------------------------------------------------------
// <summary>
//    Defines the AppDelegate type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using CodeFramework.iOS;
using System.Collections.Generic;
using System;    
using Cirrious.CrossCore;
using Cirrious.MvvmCross.Touch.Platform;
using Cirrious.MvvmCross.ViewModels;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using CodeFramework.Core.Utils;
using CodeHub.Core.Services;
using System.Threading;
using System.Threading.Tasks;

namespace CodeHub.iOS
{
    /// <summary>
    /// The UIApplicationDelegate for the application. This class is responsible for launching the 
    /// User Interface of the application, as well as listening (and optionally responding) to 
    /// application events from iOS.
    /// </summary>
    [Register("AppDelegate")]
    public class AppDelegate : MvxApplicationDelegate
    {
        /// <summary>
        /// The window.
        /// </summary>
        private UIWindow window;
		public string DeviceToken;

		/// <summary>
		/// This is the main entry point of the application.
		/// </summary>
		/// <param name="args">The args.</param>
		public static void Main(string[] args)
		{
			// if you want to use a different Application Delegate class from "AppDelegate"
			// you can specify it here.
			UIApplication.Main(args, null, "AppDelegate");
		}

        /// <summary>
        /// Finished the launching.
        /// </summary>
        /// <param name="app">The app.</param>
        /// <param name="options">The options.</param>
        /// <returns>True or false.</returns>
        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
			var iRate = MTiRate.iRate.SharedInstance;
			iRate.AppStoreID = 707173885;

			this.window = new UIWindow(UIScreen.MainScreen.Bounds);

            // Setup theme
            Theme.Setup();

            var presenter = new TouchViewPresenter(this.window);

            var setup = new Setup(this, presenter);
            setup.Initialize();

			Mvx.Resolve<CodeFramework.Core.Services.IAnalyticsService>().Init("UA-44040302-1", "CodeHub");

            var startup = Mvx.Resolve<IMvxAppStart>();
			startup.Start();

            this.window.MakeKeyAndVisible();

            InAppPurchases.Instance.PurchaseError += HandlePurchaseError;
            InAppPurchases.Instance.PurchaseSuccess += HandlePurchaseSuccess;

			if (options != null)
			{
				if (options.ContainsKey(UIApplication.LaunchOptionsRemoteNotificationKey)) 
				{
					var remoteNotification = options[UIApplication.LaunchOptionsRemoteNotificationKey] as NSDictionary;
					if(remoteNotification != null) {
						HandleNotification(remoteNotification);
					}
				}
			}

            var features = Mvx.Resolve<IFeaturesService>();

			// Notifications don't work on teh simulator so don't bother
            if (MonoTouch.ObjCRuntime.Runtime.Arch != MonoTouch.ObjCRuntime.Arch.SIMULATOR && features.IsPushNotificationsActivated)
			{
				const UIRemoteNotificationType notificationTypes = UIRemoteNotificationType.Alert | UIRemoteNotificationType.Badge;
				UIApplication.SharedApplication.RegisterForRemoteNotificationTypes(notificationTypes);
			}

            return true;
        }

        void HandlePurchaseSuccess (object sender, string e)
        {
            Mvx.Resolve<CodeFramework.Core.Services.IDefaultValueService>().Set(e, true);

            if (string.Equals(e, InAppPurchases.PushNotificationsId))
            {
                const UIRemoteNotificationType notificationTypes = UIRemoteNotificationType.Alert | UIRemoteNotificationType.Badge;
                UIApplication.SharedApplication.RegisterForRemoteNotificationTypes(notificationTypes);
            }
        }

        void HandlePurchaseError (object sender, Exception e)
        {
            MonoTouch.Utilities.ShowAlert("Unable to make purchase", e.Message);
        }

		public override void DidReceiveRemoteNotification(UIApplication application, NSDictionary userInfo, System.Action<UIBackgroundFetchResult> completionHandler)
		{
			if (application.ApplicationState == UIApplicationState.Active)
				return;
			HandleNotification(userInfo);
		}

		private void HandleNotification(NSDictionary data)
		{
			try
			{
				var viewDispatcher = Mvx.Resolve<Cirrious.MvvmCross.Views.IMvxViewDispatcher>();
				var request = MvxViewModelRequest<CodeHub.Core.ViewModels.Repositories.RepositoryViewModel>.GetDefaultRequest();
				var repoId = new RepositoryIdentifier(data["r"].ToString());
				request.ParameterValues = new Dictionary<string, string>() {{"Username", repoId.Owner}, {"Repository", repoId.Name}};
				viewDispatcher.ShowViewModel(request);
			}
			catch (Exception e)
			{
				Console.WriteLine("Handle Notifications issue: " + e);
			}
		}

		public override void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
		{
			DeviceToken = deviceToken.Description.Trim('<', '>').Replace(" ", "");

            var app = Mvx.Resolve<IApplicationService>();
            if (app.Account != null && !app.Account.IsPushNotificationsEnabled.HasValue)
            {
                Task.Run(() => Mvx.Resolve<IPushNotificationsService>().Register());
                app.Account.IsPushNotificationsEnabled = true;
                app.Accounts.Update(app.Account);
            }
		}

		public override void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error)
		{
			MonoTouch.Utilities.ShowAlert("Error Registering for Notifications", error.LocalizedDescription);
		}
    }
}