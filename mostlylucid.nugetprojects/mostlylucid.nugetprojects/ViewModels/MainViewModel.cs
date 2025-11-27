using CommunityToolkit.Mvvm.ComponentModel;

namespace mostlylucid.nugetprojects.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private string _greeting = "Welcome to Avalonia!";
}