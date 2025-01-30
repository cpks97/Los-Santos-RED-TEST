using ExtensionsMethods;
using LosSantosRED.lsr.Interface;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class ContractorInteraction : IContactMenuInteraction
{
    private IContactInteractable Player;

    private MenuPool MenuPool;
    private UIMenu ContractorMenu;
    private IGangs Gangs;
    private IPlacesOfInterest PlacesOfInterest;
    private ISettingsProvideable Settings;
    private UIMenuItem TaskCancel;
    //private UIMenuItem StartContractorMenu;
    private UIMenu JobsSubMenu;
    private ContractorContact Contact;

    public ContractorInteraction(IContactInteractable player, IGangs gangs, IPlacesOfInterest placesOfInterest, ISettingsProvideable settings, ContractorContact contact, IAgencies agencies)
    {
        Player = player;
        Gangs = gangs;
        PlacesOfInterest = placesOfInterest;
        Settings = settings;
        Contact = contact;
        MenuPool = new MenuPool();
    }
    public void Start(PhoneContact contact)
    {
        ContractorMenu = new UIMenu("", "Select an Option");
        ContractorMenu.RemoveBanner();
        MenuPool.Add(ContractorMenu);
        AddJobs();
        ContractorMenu.Visible = true;
        GameFiber.StartNew(delegate
        {
            try
            {
                while (MenuPool.IsAnyMenuOpen())
                {
                    GameFiber.Yield();
                }
                Player.CellPhone.Close(250);
            }
            catch (Exception ex)
            {
                EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                EntryPoint.ModController.CrashUnload();
            }
        }, "CellPhone");
    }
    private void AddJobs()
    {
        JobsSubMenu = MenuPool.AddSubMenu(ContractorMenu, "Jobs");
        JobsSubMenu.RemoveBanner();
        if (Player.PlayerTasks.HasTask(Contact.Name))
        {
            TaskCancel = new UIMenuItem("Cancel Task", "Call another time.") { RightLabel = "~o~$?~s~" };
            TaskCancel.Activated += (sender, e) =>
            {
                Player.PlayerTasks.CancelTask(Contact);
                sender.Visible = false;
            };
            JobsSubMenu.AddItem(TaskCancel);
            return;
        }
        AddContractorSubMenu();
    }
    private void AddContractorSubMenu()
    {
        UIMenu ContractorSubMenu = MenuPool.AddSubMenu(JobsSubMenu, "Contract Killing");
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].Description = $"Hitman for hire";
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].RightLabel = $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.ContractorPaymentMin:C0}-{Settings.SettingsManager.TaskSettings.ContractorPaymentMax:C0}~s~";
        ContractorSubMenu.RemoveBanner();
        UIMenuItem StartTaskMenu = new UIMenuItem("Start Task", "Start the task.") { RightLabel = $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.ContractorPaymentMin:C0}-{Settings.SettingsManager.TaskSettings.ContractorPaymentMax:C0}~s~" };
        StartTaskMenu.Activated += (sender, e) =>
        {
            Player.PlayerTasks.ContractorTasks.ContractorTask(Contact);
            sender.Visible = false;
        };
        ContractorSubMenu.AddItem(StartTaskMenu);
    }

    public void Update()
    {
        MenuPool.ProcessMenus();
    }
  
}

