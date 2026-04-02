namespace SwipeAndBye;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        
        // LLevar a una pagina vacia, por los permisos de la app
        MainPage = new ContentPage();
    }
}