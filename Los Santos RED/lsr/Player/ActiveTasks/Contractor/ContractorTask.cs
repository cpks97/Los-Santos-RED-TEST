using ExtensionsMethods;
using LosSantosRED.lsr.Helper;
using LosSantosRED.lsr.Interface;
using LSR.Vehicles;
using Rage;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace LosSantosRED.lsr.Player.ActiveTasks
{
    public class ContractorTask : IPlayerTask
    {
        private ITaskAssignable Player;
        private ITimeReportable Time;
        private IGangs Gangs;
        private PlayerTasks PlayerTasks;
        private IPlacesOfInterest PlacesOfInterest;
        private List<DeadDrop> ActiveDrops = new List<DeadDrop>();
        private ISettingsProvideable Settings;
        private IEntityProvideable World;
        private ICrimes Crimes;
        private IShopMenus ShopMenus;
        private PlayerTask CurrentTask;
        private int MoneyToRecieve;
        private DeadDrop myDrop;
        private IWeapons Weapons;
        private INameProvideable Names;
        private bool TargetIsMale;
        private string TargetName;
        private bool TargetIsAtHome;
        private GameLocation TargetLocation;
        private Vector3 TargetpawnPosition;
        private float TargetSpawnHeading;
        private readonly List<string> FemaleTargetPossibleModels = new List<string>() { "a_f_y_beach_01", "a_f_m_Soucent_01","a_f_y_Soucent_01", "g_f_y_families_01", "a_f_y_fitness_01", "g_f_y_ballas_01","a_f_y_business_01", "a_f_y_business_02", "a_f_y_business_03", "a_f_y_business_04",
            "a_f_y_genhot_01", "a_f_y_fitness_01", "a_f_m_business_02", "a_f_y_clubcust_01", "a_f_y_femaleagent","a_f_y_eastsa_03","a_f_y_hiker_01","a_f_y_hipster_01","a_f_y_hipster_04","a_f_y_skater_01","a_f_y_soucent_03","a_f_y_tennis_01","a_f_y_vinewood_01","a_f_y_tourist_02" };

        private readonly List<string> MaleTargetPossibleModels = new List<string>() { "csb_ramp_mex", "g_m_y_armgoon_02", "u_m_m_BankMan", "a_m_y_mexthug_01", "A_M_Y_MexThug_01", "a_m_m_business_01", "a_m_m_eastsa_02", "g_m_y_ballaorig_01", "A_M_M_SouCent_02", "a_m_y_busicas_01", "s_m_y_dealer_01", "g_m_m_armlieut_01", "a_m_m_Bevhills_02",
            "a_m_m_soucent_01", "a_m_y_business_01", "g_m_y_ballaeast_01", "a_m_y_runner_02", "g_m_y_strpunk_02", };

        private bool HasSpawnPosition => TargetSpawnPosition != Vector3.Zero;
        private int SpawnPositionCellX;
        private int SpawnPositionCellY;
        private bool IsTargetSpawned;
        private int GameTimeToWaitBeforeComplications;
        private string TargetModel;

        private PedExt Target;
        private PedVariation TargetVariation;
        private bool HasAddedComplications;
        private bool WillAddComplications;
        private object pedHeadshotHandle;
        private bool TargetIsCustomer;
        private bool WillFlee;
        private bool WillFight;
        private ShopMenu TargetShopMenu;
        private WeaponInformation TargetWeapon;
        private ContractorContact Contact;

        private bool IsPlayerFarFromTarget => Target != null && Target.Pedestrian.Exists() && Target.Pedestrian.DistanceTo2D(Player.Character) >= 850f;
        private bool IsPlayerNearTargetSpawn => SpawnPositionCellX != -1 && SpawnPositionCellY != -1 && NativeHelper.IsNearby(EntryPoint.FocusCellX, EntryPoint.FocusCellY, SpawnPositionCellX, SpawnPositionCellY, 6);

        public Vector3 TargetSpawnPosition { get; private set; }
        public bool TargetsIsCustomer { get; private set; }

        public ContractorTask(ITaskAssignable player, ITimeReportable time, IGangs gangs, PlayerTasks playerTasks, IPlacesOfInterest placesOfInterest, List<DeadDrop> activeDrops, ISettingsProvideable settings, IEntityProvideable world,
            ICrimes crimes, INameProvideable names, IWeapons weapons, IShopMenus shopMenus, ContractorContact ContractorContact)
        {
            Player = player;
            Time = time;
            Gangs = gangs;
            PlayerTasks = playerTasks;
            PlacesOfInterest = placesOfInterest;
            ActiveDrops = activeDrops;
            Settings = settings;
            World = world;
            Crimes = crimes;
            Names = names;
            Weapons = weapons;
            ShopMenus = shopMenus;
            Contact = ContractorContact;
        }

        public void Setup()
        {

        }
        public void Dispose()
        {
            if (Target != null && Target.Pedestrian.Exists())
            {
                Target.DeleteBlip();
                Target.Pedestrian.IsPersistent = false;
                Target.Pedestrian.Delete();
            }
            if (TargetLocation != null)
            {
                TargetLocation.IsPlayerInterestedInLocation = false;
            }
        }
        public void Start(ContractorContact contact)
        {
            Contact = contact;
            if (Contact == null)
            {
                return;
            }
            if (PlayerTasks.CanStartNewTask(Contact.Name))
            {
                GetPedInformation();
                if (HasSpawnPosition)
                {
                    GetPayment();
                    SendInitialInstructionsMessage();
                    AddTask();
                    GameFiber PayoffFiber = GameFiber.StartNew(delegate
                    {
                        try
                        {
                            Loop();
                            FinishTask();
                        }
                        catch (Exception ex)
                        {
                            EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                            EntryPoint.ModController.CrashUnload();
                        }
                    }, "PayoffFiber");
                }
                else
                {
                    SendTaskAbortMessage();
                }
            }
        }
        private void GetPedInformation()
        {
            TargetIsMale = RandomItems.RandomPercent(60);
            TargetName = Names.GetRandomName(TargetIsMale);
            TargetIsAtHome = RandomItems.RandomPercent(30);
            if (TargetIsAtHome)
            {
                TargetLocation = PlacesOfInterest.PossibleLocations.Residences.Where(x => !x.IsOwnedOrRented && x.IsCorrectMap(World.IsMPMapLoaded) && x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState)).PickRandom();
            }
            else
            {
                TargetLocation = PlacesOfInterest.PossibleLocations.HitTaskLocations().Where(x => x.IsCorrectMap(World.IsMPMapLoaded) && x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState)).PickRandom();
            }

            TargetVariation = null;
            if (TargetIsMale)
            {
                TargetModel = MaleTargetPossibleModels.Where(x => Player.ModelName.ToLower() != x.ToLower()).PickRandom();
            }
            else
            {
                TargetModel = FemaleTargetPossibleModels.Where(x => Player.ModelName.ToLower() != x.ToLower()).PickRandom();
            }
            if (TargetLocation != null)
            {
                TargetSpawnPosition = TargetLocation.EntrancePosition;
                TargetSpawnHeading = TargetLocation.EntranceHeading;
                SpawnPositionCellX = (int)(TargetSpawnPosition.X / EntryPoint.CellSize);
                SpawnPositionCellY = (int)(TargetSpawnPosition.Y / EntryPoint.CellSize);
            }
            else
            {
                TargetSpawnPosition = Vector3.Zero;
                SpawnPositionCellX = -1;
                SpawnPositionCellY = -1;
            }
        }
        private void Loop()
        {
            while (true)
            {
                if (CurrentTask == null || !CurrentTask.IsActive)
                {
                    //EntryPoint.WriteToConsoleTestLong($"Task Inactive for {Contact.Name}");
                    break;
                }
                if (!IsTargetSpawned && IsPlayerNearTargetSpawn)
                {
                    IsTargetSpawned = SpawnTarget();
                }
                if (IsTargetSpawned && IsPlayerFarFromTarget)
                {
                    DespawnTarget();
                    if (Target.HasSeenPlayerCommitCrime)
                    {
                        //EntryPoint.WriteToConsoleTestLong("Target Elimination TARGET FLED");
                        Game.DisplayHelp($"{Contact.Name} The Target fled");
                        break;
                    }
                }
                else if (IsTargetSpawned && Target != null && Target.Pedestrian.Exists() && Target.Pedestrian.IsDead)
                {
                    Target.Pedestrian.IsPersistent = false;
                    Target.DeleteBlip();
                    //EntryPoint.WriteToConsoleTestLong("Target Elimination TARGET WAS KILLED");
                    CurrentTask.OnReadyForPayment(true);
                    break;
                }
                if (IsTargetSpawned && Target != null && !Target.Pedestrian.Exists())//somehow it got removed, set it as despawned
                {
                    DespawnTarget();
                }
                GameFiber.Sleep(1000);
            }
        }
        private void FinishTask()
        {
            if (TargetLocation != null)
            {
                TargetLocation.IsPlayerInterestedInLocation = false;
            }
            if (CurrentTask != null && CurrentTask.IsActive && CurrentTask.IsReadyForPayment)
            {
                //GameFiber.Sleep(RandomItems.GetRandomNumberInt(5000, 10000));

                StartDeadDropPayment();//sets u teh whole dead drop thingamajic
            }
            else if (CurrentTask != null && CurrentTask.IsActive)
            {
                //GameFiber.Sleep(RandomItems.GetRandomNumberInt(5000, 10000));
                SetFailed();
            }
            else
            {
                Dispose();
            }
        }
        private void SetCompleted()
        {
            //EntryPoint.WriteToConsoleTestLong("Target Elimination COMPLETED");

            PlayerTasks.CompleteTask(Contact, true);

            SendCompletedMessage();
        }
        private void SetFailed()
        {
            //EntryPoint.WriteToConsoleTestLong("Target Elimination FAILED");
            SendFailMessage();
            PlayerTasks.FailTask(Contact);
        }
        private void StartDeadDropPayment()
        {
            myDrop = PlacesOfInterest.GetUsableDeadDrop(World.IsMPMapLoaded, Player.CurrentLocation);
            if (myDrop != null)
            {
                myDrop.SetupDrop(MoneyToRecieve, false);
                ActiveDrops.Add(myDrop);
                SendDeadDropStartMessage();
                while (true)
                {
                    if (CurrentTask == null || !CurrentTask.IsActive)
                    {
                        //EntryPoint.WriteToConsoleTestLong($"Task Inactive for {Contact.Name}");
                        break;
                    }
                    if (myDrop.InteractionComplete)
                    {
                        //EntryPoint.WriteToConsoleTestLong($"Picked up money for Contractor for {Contact.Name}");
                        Game.DisplayHelp($"{Contact.Name} Money Picked Up");
                        break;
                    }
                    GameFiber.Sleep(1000);
                }
                if (CurrentTask != null && CurrentTask.IsActive && CurrentTask.IsReadyForPayment)
                {
                    PlayerTasks.CompleteTask(Contact, true);
                }
                myDrop?.Reset();
                myDrop?.Deactivate(true);
            }
            else
            {

                PlayerTasks.CompleteTask(Contact, true);
                SendQuickPaymentMessage();
            }
        }
        private void AddTask()
        {
            //EntryPoint.WriteToConsoleTestLong($"You are hired to kill a target!");
            PlayerTasks.AddTask(Contact, 0, 2000, 0, -500, 7, "Contracted Killing");
            CurrentTask = PlayerTasks.GetTask(Contact.Name);
            IsTargetSpawned = false;
            GameTimeToWaitBeforeComplications = RandomItems.GetRandomNumberInt(3000, 10000);
            HasAddedComplications = false;
            WillAddComplications = RandomItems.RandomPercent(Settings.SettingsManager.TaskSettings.ContractorComplicationsPercentage);
            WillFlee = false;
            WillFight = false;
            if (WillAddComplications)
            {
                if (RandomItems.RandomPercent(50))
                {
                    WillFlee = true;
                }
                else
                {
                    WillFight = true;
                }
            }
            TargetWeapon = null;
            if (RandomItems.RandomPercent(40))
            {
                Weapons.GetRandomRegularWeapon(WeaponCategory.Melee);
            }
            else
            {
                if (RandomItems.RandomPercent(50))
                {
                    Weapons.GetRandomRegularWeapon(WeaponCategory.Pistol);
                }
                else
                {
                    if (RandomItems.RandomPercent(50))
                    {
                        Weapons.GetRandomRegularWeapon(WeaponCategory.AR);
                    }
                    else
                    {
                        Weapons.GetRandomRegularWeapon(WeaponCategory.Shotgun);
                    }
                }
            }
            TargetShopMenu = null;
            TargetIsCustomer = RandomItems.RandomPercent(30f);
            if (TargetIsCustomer)
            {
                TargetShopMenu = ShopMenus.GetRandomDrugCustomerMenu();
            }

            if (TargetLocation != null)
            {
                TargetLocation.IsPlayerInterestedInLocation = true;
            }
        }
        private bool SpawnTarget()
        {
            if (TargetSpawnPosition != Vector3.Zero)
            {
                World.Pedestrians.CleanupAmbient();
                Ped ped = new Ped(TargetModel, TargetSpawnPosition, TargetSpawnHeading);
                GameFiber.Yield();
                NativeFunction.Natives.SET_MODEL_AS_NO_LONGER_NEEDED(Game.GetHashKey(TargetModel));
                if (ped.Exists())
                {
                    string GroupName = "Man";
                    if (!TargetIsMale)
                    {
                        GroupName = "Woman";
                    }
                    Target = new PedExt(ped, Settings, Crimes, Weapons, TargetName, GroupName, World);
                    if (Settings.SettingsManager.TaskSettings.ShowEntityBlips)
                    {
                        Target.AddBlip();
                    }
                    World.Pedestrians.AddEntity(Target);
                    Target.WasEverSetPersistent = true;
                    Target.CanBeAmbientTasked = true;
                    Target.CanBeTasked = true;
                    Target.WasModSpawned = true;
                    if (TargetVariation == null)
                    {
                        Target.Pedestrian.RandomizeVariation();
                        TargetVariation = NativeHelper.GetPedVariation(Target.Pedestrian);
                    }
                    else
                    {
                        TargetVariation.ApplyToPed(Target.Pedestrian);
                    }
                    pedHeadshotHandle = NativeFunction.Natives.REGISTER_PEDHEADSHOT<int>(ped);
                    if (TargetIsCustomer)
                    {
                        Target.SetupTransactionItems(TargetShopMenu, false);
                    }
                    if (WillAddComplications)
                    {
                        ped.RelationshipGroup = RelationshipGroup.HatesPlayer;
                        if (WillFlee)//flee
                        {
                            Target.WillCallPolice = true;
                            Target.WillCallPoliceIntense = true;
                            Target.WillFight = false;
                            Target.WillFightPolice = false;
                            Target.WillAlwaysFightPolice = false;
                            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_AlwaysFlee, true);
                            NativeFunction.Natives.SET_PED_FLEE_ATTRIBUTES(ped, 2, true);
                            //EntryPoint.WriteToConsoleTestLong("TARGET ELIMINATION, THE TARGET WITH FLEE FROM YOU");
                        }
                        else if (WillFight)
                        {
                            Target.WillFight = true;
                            Target.WillCallPolice = false;
                            Target.WillCallPoliceIntense = false;
                            Target.WillFightPolice = true;
                            Target.WillAlwaysFightPolice = true;
                            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_AlwaysFight, true);
                            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_CanFightArmedPedsWhenNotArmed, true);
                            NativeFunction.Natives.SET_PED_FLEE_ATTRIBUTES(ped, 0, false);

                            if (TargetWeapon != null)
                            {
                                NativeFunction.Natives.GIVE_WEAPON_TO_PED(ped, (uint)TargetWeapon.Hash, TargetWeapon.AmmoAmount, false, false);
                            }
                            //EntryPoint.WriteToConsoleTestLong("TARGET ELIMINATION, THE TARGET WITH FIGHT YOU");
                        }
                        //they either know and flee, or know and fight     
                    }
                    GameFiber.Sleep(1000);
                    SendTargetSpawnedMessage();
                    return true;
                }
            }
            return false;
        }
        private void SendTargetSpawnedMessage()
        {
            List<string> Replies;
            string LookingForItem = "";
            if (TargetsIsCustomer)
            {
                MenuItem myMenuItem = Target.ShopMenu?.Items.Where(x => x.NumberOfItemsToPurchaseFromPlayer > 0 && x.IsIllicilt).PickRandom();
                if (myMenuItem != null)
                {
                    LookingForItem = myMenuItem.ModItemName;
                }
            }
            if (NativeFunction.Natives.IsPedheadshotReady<bool>(pedHeadshotHandle))
            {
                Replies = new List<string>() {
                    $"Picture of ~y~{TargetName}~s~ attached. They're near ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                    $"Sent you a picture of ~y~{TargetName}~s~. They are at ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                    $"~y~{TargetName}~s~. They're hanging around ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                    $"Their name is ~y~{TargetName}~s~, picture is attached. They're at ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                    $"Remember, ~y~{TargetName}~s~ is their name. I've sent a picture. I hear they are still around ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                     };
                string PickedReply = Replies.PickRandom();
                if (TargetIsCustomer && LookingForItem != "")
                {
                    List<string> ItemReplies = new List<string>() {
                    $" They are probably looking for ~p~{LookingForItem}~s~.",
                    $" They like ~p~{LookingForItem}~s~.",
                    $" Will be looking to buy ~p~{LookingForItem}~s~.",
                    $" They are interested in ~p~{LookingForItem}~s~.",
                    $" The target likes ~p~{LookingForItem}~s~.",
                     };
                    PickedReply += ItemReplies.PickRandom();
                }

                if (WillFight || WillFlee)
                {
                    PickedReply += " ~s~Your target has gotten word about the hit, he knows about you. Take them out!";
                }
                string str = NativeFunction.Natives.GET_PEDHEADSHOT_TXD_STRING<string>(pedHeadshotHandle);
                EntryPoint.WriteToConsole($"TARGET CONTRACT SENT PICTURE MESSAGE {str}");
                Player.CellPhone.AddCustomScheduledText(Contact, PickedReply, Time.CurrentDateTime, str, true);
            }
            else
            {
                Replies = new List<string>() {
                    $"~y~{TargetName}~s~. I hear they're near ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                    $"~y~{TargetName}~s~. They should still be around ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                    $"~y~{TargetName}~s~. They are still around ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                    $"The name is ~y~{TargetName}~s~. They are hanging around ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                    $"Remember, ~y~{TargetName}~s~ is the targets name. I got word they are still around ~p~{TargetLocation.Name} {TargetLocation.FullStreetAddress}~s~.",
                     };
                string PickedReply = Replies.PickRandom();
                if (TargetIsCustomer && LookingForItem != "")
                {
                    List<string> ItemReplies = new List<string>() {
                    $" They are probably looking for ~p~{LookingForItem}~s~.",
                    $" They like ~p~{LookingForItem}~s~.",
                    $" Will be looking to buy ~p~{LookingForItem}~s~.",
                    $" They are interested in ~p~{LookingForItem}~s~.",
                    $" The target likes ~p~{LookingForItem}~s~.",
                     };
                    PickedReply += ItemReplies.PickRandom();
                }

                if (WillFight || WillFlee)
                {
                    PickedReply += " ~s~Your target has gotten word about the hit, he knows about you. Take them out!";
                }
                EntryPoint.WriteToConsole("TARGET CONTRACT SENT REGULAR MESSAGE");
                Player.CellPhone.AddCustomScheduledText(Contact, PickedReply, Time.CurrentDateTime, null, true);
            }
        }
        private void DespawnTarget()
        {
            if (Target != null && Target.Pedestrian.Exists())
            {
                Target.DeleteBlip();
                Target.Pedestrian.Delete();
                //EntryPoint.WriteToConsoleTestLong("Target Elimination DESPAWNED TARGET");
            }
            IsTargetSpawned = false;
        }
        private void GetPayment()
        {
            MoneyToRecieve = RandomItems.GetRandomNumberInt(Settings.SettingsManager.TaskSettings.ContractorPaymentMin, Settings.SettingsManager.TaskSettings.ContractorPaymentMax).Round(500);
            if (MoneyToRecieve <= 0)
            {
                MoneyToRecieve = 500;
            }
        }
        private void SendTaskAbortMessage()
        {
            List<string> Replies = new List<string>() {
                    "I've got no contracts yet, I'll call you back",
                    "I've hits for you yet",
                    "Give me a few days",
                    "Not a lot to be done right now",
                    "I'll let you know when I've got a job for you",
                    "Call me later.",
                    };
            Player.CellPhone.AddPhoneResponse(Contact.Name, Replies.PickRandom());
        }
        private void SendInitialInstructionsMessage()
        {
            List<string> Replies;
            if (TargetIsAtHome)
            {
                Replies = new List<string>() {
                    $"Got someone that needs to disappear. Their location is ~p~{TargetLocation.FullStreetAddress}~s~. Name ~y~{TargetName}~s~. ${MoneyToRecieve}",
                    $"I got a hit for you, they live at ~p~{TargetLocation.FullStreetAddress}~s~ their name is ~y~{TargetName}~s~. ${MoneyToRecieve} once the jobs done",
                    $"Theirs a hit out at ~p~{TargetLocation.FullStreetAddress}~s~. Their name is ~y~{TargetName}~s~. Payment of ${MoneyToRecieve}",
                    $"~y~{TargetName}~s~ is living at ~p~{TargetLocation.FullStreetAddress}~s~. They should be home. Take them out. ${MoneyToRecieve}",
                    $"I need a house painting, their name is ~y~{TargetName}~s~ pay them a visit, they live at ~p~{TargetLocation.FullStreetAddress}~s~. ${MoneyToRecieve}",
                     };
            }
            else
            {
                Replies = new List<string>() {
                    $"Got a hit I need you to take care of. Target hangs around ~p~{TargetLocation.Name}~s~. Address is ~p~{TargetLocation.FullStreetAddress}~s~. Name ~y~{TargetName}~s~. ${MoneyToRecieve}",
                    $"Need you to go to ~p~{TargetLocation.Name}~s~ on ~p~{TargetLocation.FullStreetAddress}~s~ and get rid of ~y~{TargetName}~s~. ${MoneyToRecieve} once the jobs done",
                    $"Got a contract for you at ~p~{TargetLocation.Name}~s~ ~p~{TargetLocation.FullStreetAddress}~s~. Their name is ~y~{TargetName}~s~. Payment of ${MoneyToRecieve}",
                    $"~y~{TargetName}~s~ is at ~p~{TargetLocation.Name}~s~, address is ~p~{TargetLocation.FullStreetAddress}~s~. Take them out for me, will ya. ${MoneyToRecieve}",
                    $"Got this guy, name's ~y~{TargetName}~s~ I need him gone, he's at ~p~{TargetLocation.Name}~s~ on ~p~{TargetLocation.FullStreetAddress}~s~. ${MoneyToRecieve}",
                     };
            }

            Player.CellPhone.AddPhoneResponse(Contact.Name, Replies.PickRandom());
        }
        private void SendQuickPaymentMessage()
        {
            List<string> Replies = new List<string>() {
                            $"Pleasure doing business with you ${MoneyToRecieve}",
                            $"Thanks for doing that thing for me. Give me a call soon ${MoneyToRecieve}",
                            $"Money has been dropped, thanks ${MoneyToRecieve}",
                            $"Sending ${MoneyToRecieve}",
                            $"Nice work, money has been dropped off ${MoneyToRecieve}",
                            };
            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }
        private void SendDeadDropStartMessage()
        {
            List<string> Replies = new List<string>() {
                            $"Pickup your payment of ${MoneyToRecieve} from {myDrop.FullStreetAddress}, its {myDrop.Description}.",
                            $"Go get your payment of ${MoneyToRecieve} from {myDrop.Description}, address is {myDrop.FullStreetAddress}.",
                            };

            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }
        private void SendCompletedMessage()
        {
            List<string> Replies = new List<string>() {
                        $"You did good, I got cash waiting for you ${MoneyToRecieve}",
                        $"Thanks for taking care of business, got the cash waiting for you ${MoneyToRecieve}",
                        $"I've sent someone to drop your money off ${MoneyToRecieve}",
                        $"Got cash waiting for you ${MoneyToRecieve}",
                        $"I heard you took care of business, give me another call soon ${MoneyToRecieve}",
                        };
            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }
        private void SendFailMessage()
        {
            List<string> Replies = new List<string>() {
                        $"You spooked them, they are gone. Thanks for nothing.",
                        $"You fucked up",
                        $"This is the last time I hit you up with a job!",
                        $"How did you fuck this up so bad, they got away!",
                        $"Great, they got away. They've gone right to the cops.",
                        };
            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }
    }

}