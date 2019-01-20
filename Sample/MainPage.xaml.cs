using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Camera;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using Xamarin.Forms;

namespace Sample
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RequestPermission(Permission.Camera);
            try
            {
                var camera = CameraManager.Current.GetCamera(LogicalCameras.Front);
                camera.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            const string message = "Camera opened";
            Debug.WriteLine(message);
            Label.Text = message;
        }

        private async Task RequestPermission(Permission permission)
        {
            try
            {
                var status = await CrossPermissions.Current.CheckPermissionStatusAsync(permission);
                if (status != PermissionStatus.Granted)
                    await CrossPermissions.Current.RequestPermissionsAsync(permission);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
}