﻿using ExtensionsMethods;
using LosSantosRED.lsr.Helper;
using LosSantosRED.lsr.Interface;
using Rage;
using RAGENativeUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class PlayerTasks
{
    private ITaskAssignable Player;
    private ITimeControllable Time;
    private IGangs Gangs;
    private IPlacesOfInterest PlacesOfInterest;
    private IEntityProvideable World;
    private ICrimes Crimes;
    private INameProvideable Names;
    private IWeapons Weapons;
    private IShopMenus ShopMenus;
    private IPedGroups PedGroups;
    private List<DeadDrop> ActiveDrops = new List<DeadDrop>();
    private ISettingsProvideable Settings;
    private List<PlayerTask> LastContactTask = new List<PlayerTask>();

    private List<IPlayerTaskGroup> PlayerTaskGroups = new List<IPlayerTaskGroup>();
    public GangTasks GangTasks { get; private set; }
    public CorruptCopTasks CorruptCopTasks { get; private set; }
    public ContractorTasks ContractorTasks { get; private set; }
    public UndergroundGunsTasks UndergroundGunsTasks { get; private set; }
    public VehicleExporterTasks VehicleExporterTasks { get; private set; }
    public List<PlayerTask> PlayerTaskList { get; set; } = new List<PlayerTask>();
    public PlayerTasks(ITaskAssignable player, ITimeControllable time, IGangs gangs, IPlacesOfInterest placesOfInterest, ISettingsProvideable settings, IEntityProvideable world, 
        ICrimes crimes, INameProvideable names, IWeapons weapons, IShopMenus shopMenus, IModItems modItems, IPedGroups pedGroups, IAgencies agencies, IGangTerritories gangTerritories, IZones zones)
    {
        Player = player;
        Time = time;
        Gangs = gangs;
        PlacesOfInterest = placesOfInterest;
        Settings = settings;
        World = world;
        Crimes = crimes;
        Names = names;
        Weapons = weapons;
        ShopMenus = shopMenus;
        PedGroups = pedGroups;
        GangTasks = new GangTasks(Player,Time,Gangs,this,PlacesOfInterest, ActiveDrops, Settings,World,Crimes, modItems, ShopMenus, Weapons,Names,PedGroups, agencies, gangTerritories, zones);
        CorruptCopTasks = new CorruptCopTasks(Player, Time, Gangs, this, PlacesOfInterest, ActiveDrops, Settings, World, Crimes, Names, Weapons, ShopMenus);
        ContractorTasks = new ContractorTasks(Player, Time, Gangs, this, PlacesOfInterest, ActiveDrops, Settings, World, Crimes, Names, Weapons, ShopMenus);
        UndergroundGunsTasks = new UndergroundGunsTasks(Player, Time, Gangs, this, PlacesOfInterest, ActiveDrops, Settings, World, Crimes);
        VehicleExporterTasks = new VehicleExporterTasks(Player, Time, Gangs, this, PlacesOfInterest, ActiveDrops, Settings, World, Crimes, modItems);
        PlayerTaskGroups = new List<IPlayerTaskGroup>
        {
            GangTasks,
            CorruptCopTasks,
            ContractorTasks,
            UndergroundGunsTasks,
            VehicleExporterTasks
        };
    }
    public void Setup()
    {
        foreach(IPlayerTaskGroup playerTaskGroup in PlayerTaskGroups)
        {
            playerTaskGroup.Setup();
        }
    }
    public void Update()
    {
        PlayerTaskList.RemoveAll(x => !x.IsActive);
        foreach(PlayerTask pt in PlayerTaskList.ToList())
        {
            if(pt != null && pt.CanExpire && DateTime.Compare(pt.ExpireTime, Time.CurrentDateTime) < 0)
            {
                ExpireTask(pt);
            }
            else if (pt != null && pt.CanExpire && pt.DaysToCompleted >= 2 && !pt.HasSentExpiringSoon && DateTime.Compare(pt.ExpireTime.AddDays(-1), Time.CurrentDateTime) < 0)
            {
                pt.HasSentExpiringSoon = true;
                SendExpiringSoonMessage(pt);
            }
        }
    }
    public void Reset()
    {
        Dispose();//Clear();
    }
    public void Clear()
    {
        PlayerTaskList.Clear();
        LastContactTask.Clear();
    }
    public void Dispose()
    {
        foreach (PlayerTask pt in PlayerTaskList.ToList())
        {
            pt.IsActive = false;
        }
        PlayerTaskList.Clear();
        foreach (IPlayerTaskGroup playerTaskGroup in PlayerTaskGroups)
        {
            playerTaskGroup.Dispose();
        }
        LastContactTask.Clear();
    }
    public void OnStandardRespawn()
    {
        List<PhoneContact> contacts = PlayerTaskList.Where(x => x.FailOnStandardRespawn).Select(y => y.PhoneContact).ToList();
        contacts.ForEach(x => FailTask(x));
    }
    public void CancelTask(PhoneContact phoneContact)
    {
        PlayerTask currentAssignment = GetTask(phoneContact.Name);
        if (currentAssignment != null)
        {
            FailTask(phoneContact);
            SendTaskFailMessage(phoneContact.Name);
        }
    }
    public void CompleteTask(PhoneContact phoneContact, bool addToLast)
    {
        if(phoneContact == null)
        {
            return;
        }
        PlayerTask myTask = PlayerTaskList.FirstOrDefault(x => x.ContactName == phoneContact.Name && x.IsActive);
        if(myTask != null)
        {
            EntryPoint.WriteToConsole($"CompleteTask: FOR : {phoneContact.Name}");
            Player.RelationshipManager.SetCompleteTask(phoneContact, myTask.RepAmountOnCompletion, myTask.JoinGangOnComplete);
            if (myTask.PaymentAmountOnCompletion != 0)
            {
                Player.BankAccounts.GiveMoney(myTask.PaymentAmountOnCompletion, false);
            }
            myTask.OnCompleted();
            myTask.IsActive = false;
            myTask.IsReadyForPayment = false;
            myTask.WasCompleted = true;
            myTask.CompletionTime = Time.CurrentDateTime;
            //EntryPoint.WriteToConsoleTestLong($"Task Completed for {contactName}");
            if (Settings.SettingsManager.TaskSettings.DisplayHelpPrompts)
            {
                Game.DisplayHelp($"Task Completed for {phoneContact.Name}");
            }
            LastContactTask.RemoveAll(x => x.ContactName == phoneContact.Name);
            if (addToLast)
            {
                LastContactTask.Add(myTask);
            }
        }
        Player.CellPhone.ClearPendingTexts(phoneContact);
        PlayerTaskList.RemoveAll(x => x.ContactName == phoneContact.Name);
    }
    public void FailTask(PhoneContact phoneContact)
    {
        if (phoneContact == null)
        {
            return;
        }
        PlayerTask myTask = PlayerTaskList.FirstOrDefault(x => x.ContactName == phoneContact.Name && x.IsActive);
        if (myTask != null)
        {

            if (myTask.IsReadyForPayment)
            {
                if (Settings.SettingsManager.TaskSettings.DisplayHelpPrompts)
                {
                    Game.DisplayHelp($"Task Expired for {phoneContact.Name}");
                }
            }
            else
            {
                Player.RelationshipManager.SetFailedTask(phoneContact, myTask.RepAmountOnFail, myTask.DebtAmountOnFail);
                if (Settings.SettingsManager.TaskSettings.DisplayHelpPrompts)
                {
                    Game.DisplayHelp($"Task Failed for {phoneContact.Name}");
                }
            }
            myTask.IsActive = false;
            myTask.IsReadyForPayment = false;
            myTask.WasFailed = true;
            myTask.FailedTime = Time.CurrentDateTime;
            //EntryPoint.WriteToConsoleTestLong($"Task Failed for {contactName}");

            LastContactTask.RemoveAll(x => x.ContactName == phoneContact.Name);
            LastContactTask.Add(myTask);
        }
        Player.CellPhone.ClearPendingTexts(phoneContact);
        PlayerTaskList.RemoveAll(x => x.ContactName == phoneContact.Name);
    }
    public bool HasTask(string contactName)
    {
        return PlayerTaskList.Any(x => x.ContactName.ToLower() == contactName.ToLower() && x.IsActive);
    }
    //public void AddTask(string contactName, int moneyOnCompletion, int repOnCompletion, int debtOnFail, int repOnFail, string taskName)
    //{
    //    if (!PlayerTaskList.Any(x => x.ContactName == contactName && x.IsActive))
    //    {
    //        PlayerTaskList.Add(new PlayerTask(contactName, true, Settings) { Name = taskName, PaymentAmountOnCompletion = moneyOnCompletion, RepAmountOnCompletion = repOnCompletion, DebtAmountOnFail = debtOnFail, RepAmountOnFail = repOnFail, StartTime = Time.CurrentDateTime });
    //    }
    //}
    public void AddTask(PhoneContact phoneContact, int moneyOnCompletion, int repOnCompletion, int debtOnFail, int repOnFail, int daysToComplete, string taskName, bool joinGangOnComplete)
    {
        if (!PlayerTaskList.Any(x => x.ContactName == phoneContact.Name && x.IsActive))
        {
            PlayerTaskList.Add(new PlayerTask(phoneContact.Name, true, Settings) { PhoneContact = phoneContact, JoinGangOnComplete = joinGangOnComplete, Name = taskName, PaymentAmountOnCompletion = moneyOnCompletion, RepAmountOnCompletion = repOnCompletion, DebtAmountOnFail = debtOnFail, RepAmountOnFail = repOnFail, CanExpire = true, ExpireTime = Time.CurrentDateTime.AddDays(daysToComplete), StartTime = Time.CurrentDateTime });
        }
    }
    public void AddTask(PhoneContact phoneContact, int moneyOnCompletion, int repOnCompletion, int debtOnFail, int repOnFail, int daysToComplete, string taskName)
    {
        if (!PlayerTaskList.Any(x => x.ContactName == phoneContact.Name && x.IsActive))
        {
            PlayerTaskList.Add(new PlayerTask(phoneContact.Name, true, Settings) { PhoneContact = phoneContact, Name = taskName, PaymentAmountOnCompletion = moneyOnCompletion, RepAmountOnCompletion = repOnCompletion, DebtAmountOnFail = debtOnFail, RepAmountOnFail = repOnFail, CanExpire = true, ExpireTime = Time.CurrentDateTime.AddDays(daysToComplete), StartTime = Time.CurrentDateTime });
        }
    }
    public void AddQuickTask(PhoneContact phoneContact, int moneyOnCompletion, int repOnCompletion, int debtOnFail, int repOnFail, int hoursToComplete, string taskName)
    {
        if (!PlayerTaskList.Any(x => x.ContactName == phoneContact.Name && x.IsActive))
        {
            PlayerTaskList.Add(new PlayerTask(phoneContact.Name, true, Settings) { PhoneContact = phoneContact, Name = taskName, PaymentAmountOnCompletion = moneyOnCompletion, RepAmountOnCompletion = repOnCompletion, DebtAmountOnFail = debtOnFail, RepAmountOnFail = repOnFail, CanExpire = true, ExpireTime = Time.CurrentDateTime.AddHours(hoursToComplete), StartTime = Time.CurrentDateTime });
        }
    }
    public void RemoveTask(string contactName)
    {
        PlayerTaskList.RemoveAll(x => x.ContactName == contactName);
    }
    public PlayerTask GetTask(string contactName)
    {
        return PlayerTaskList.FirstOrDefault(x => x.ContactName == contactName);
    }
    public bool CanStartNewTask(string contactName)
    {
        if(HasTask(contactName))
        {
            SendAlreadyHasTaskMessage(contactName);
            return false;
        }
        if(RecentlyEndedTask(contactName))
        {
            SendRecentlyEndedTaskMessage(contactName);
            return false;
        }
        return true;
    }
    private bool RecentlyEndedTask(string contactName)
    {
        PlayerTask lastTask = LastContactTask.FirstOrDefault(x => x.ContactName.ToLower() == contactName.ToLower());
        if(lastTask == null)
        {
            return false;
        }
        else
        {
            if(lastTask.WasCompleted)
            {
                if (DateTime.Compare(lastTask.CompletionTime.AddHours(Settings.SettingsManager.PlayerOtherSettings.HoursBetweenTasksWhenCompleted), Time.CurrentDateTime) < 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else if (lastTask.WasFailed)
            {
                if (DateTime.Compare(lastTask.FailedTime.AddHours(Settings.SettingsManager.PlayerOtherSettings.HoursBetweenTasksWhenFailed), Time.CurrentDateTime) < 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }
    }
    private void ExpireTask(PlayerTask pt)
    {
        if (pt != null)
        {

            FailTask(pt.PhoneContact);
            SendTaskExpiredMessage(pt);
        }
    }
    private void SendTaskExpiredMessage(PlayerTask pt)
    {
        List<string> Replies = new List<string>() {
                    "You were supposed to take care of that thing this century, forget about it.",
                    "That thing we talked about? Time expired.",
                    "Too late on that thing, forget about it.",
                    "Needed you to get to that thing quicker",
                    "Forget about the work item, I had someone else take care of it.",
                    "Are you always this slow? I'll get someone else to handle things.",
                    };
        Player.CellPhone.AddPhoneResponse(pt.ContactName, Replies.PickRandom());
    }
    private void SendExpiringSoonMessage(PlayerTask pt)
    {
        List<string> Replies = new List<string>() {
                    $"Get going on that thing, it needs to be done by {pt.ExpireTime:g}.",
                    $"That thing we talked about, I need it done by {pt.ExpireTime:g}.",
                    $"Go do that thing before its too late {pt.ExpireTime:g}.",
                    $"Its almost {pt.ExpireTime:g}",
                    $"Remeber that thing? {pt.ExpireTime:g} is the deadline.",
                    $"Go get that thing done before you really piss me off. {pt.ExpireTime:g}.",
                    };
        Player.CellPhone.AddPhoneResponse(pt.ContactName, Replies.PickRandom());
    }
    private void SendTaskFailMessage(string contactName)
    {
        List<string> Replies = new List<string>() {
                    "I knew you were reliable",
                    "You really fucked me on this one",
                    "Complete waste of my time, go fuck yourself",
                    "This is a great time to fuck me like this prick",
                    "You can't even complete a simple task. Useless.",
                    "Sorry I stuck my neck out for you",
                    };
        Player.CellPhone.AddPhoneResponse(contactName, Replies.PickRandom());
    }
    private void SendAlreadyHasTaskMessage(string contactName)
    {
        List<string> Replies = new List<string>() {
                    $"Aren't you already taking care of that thing for us?",
                    $"Didn't we already give you something to do?",
                    $"Finish your task before you call us again prick.",
                    $"Get going on that thing, stop calling me",
                    $"I alredy told you what to do, stop calling me.",
                    $"You already have an item, stop with the calls.",

                    };
        Player.CellPhone.AddPhoneResponse(contactName, Replies.PickRandom());
    }
    private void SendRecentlyEndedTaskMessage(string contactName)
    {
        List<string> Replies = new List<string>() {
                    $"Let the heat die down for a bit. Give me a call tomorrow.",
                    $"Didn't you just get done with that thing? Give us some time.",
                    $"You should lay low for a bit after that thing. Call us in a few.",
                    $"We just got done with you give us some time.",
                    $"You already took care of that thing, give us a few.",
                    $"We just gave you work, don't get greedy.",
                    };
        Player.CellPhone.AddPhoneResponse(contactName, Replies.PickRandom());
    }

    public void OnInteractionMenuCreated(GameLocation gameLocation, MenuPool menuPool, UIMenu interactionMenu)
    {
        PlayerTaskGroups.ForEach(x => x.OnInteractionMenuCreated(gameLocation, menuPool, interactionMenu));
    }
}
