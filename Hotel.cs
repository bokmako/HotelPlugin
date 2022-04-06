using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Data;
using System.IO;
using System.Text;
using System.Net;
using Microsoft.Xna.Framework;

// Terraria related API References
using Newtonsoft.Json;

using TerrariaApi.Server;
using Terraria;

using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Journal;


namespace Hotel
{
    [ApiVersion(2, 1)]
    public class Hotel : TerrariaPlugin
    {
        public String SavePath = TShock.SavePath;
        private Config config;
        internal static string filepath { get { return Path.Combine(TShock.SavePath, "Hotel.json"); } }
        private void OnHotelUpdate(EventArgs args)
        {
            foreach(var r in config.Rooms)
            {
                if(r.owner != "all" && r.RentTime < DateTime.UtcNow && r.owner != "none")
                {
                    r.owner = "none";
                    File.WriteAllText(filepath, JsonConvert.SerializeObject(config, Formatting.Indented));
                }
            }
        }

        public override string Author
        {
            get { return "Bokmako"; }
        }
        public override string Description
        {
            get { return "SEconomy based Hotel"; }
        }

        public override string Name
        {
            get { return "Hotel"; }
        }

        public override Version Version
        {
            get { return new Version(0, 0, 0, 1); }
        }
        public Hotel(Main game)
            : base(game)
        {
        }
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
        }
     
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
            }
            base.Dispose(disposing);
        }

        private void OnInitialize(EventArgs args)
        {
            ReadConfig(filepath, Config.NewConfig(), out config);
            Commands.ChatCommands.Add(new Command("hotel.main", HotelMain, "hotel"));
            Commands.ChatCommands.Add(new Command("hotel.admin", HotelAdmin, "room"));

            GetDataHandlers.PlayerUpdate += OnPlayerUpdateHotel;
            ServerApi.Hooks.GameUpdate.Register(this, OnHotelUpdate);
            GetDataHandlers.ChestOpen += OnChestOpen;
            TShockAPI.Hooks.GeneralHooks.ReloadEvent += OnReload;
        }
        private void OnChestOpen(object sender, GetDataHandlers.ChestOpenEventArgs args)
        {
            Room vRoom = null;
            foreach(var r in config.Rooms)
            {
                if(r.owner == args.Player.Name)
                {
                    vRoom = r;
                    break;
                }
            }
            if (!args.Player.HasBuildPermission(args.X, args.Y) && RoomChest(args.X, args.Y, args.Player))
            {
                TShock.Log.ConsoleDebug("Bouncer / OnChestOpen rejected from region check from {0}", args.Player.Name);
                args.Handled = true;
                return;
            }
        }
        private bool RoomChest(int x, int y, TSPlayer p)
        {
            var reg = TShock.Regions.InAreaRegionName(x, y);    
            if (reg == null || reg.Count() == 0)
                return false;
            foreach(var r in reg)
            {
                foreach(var d in config.Rooms)
                {
                    if(d.RoomName == r)
                    {
                        if(p.Name != d.owner)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
            
        }
        private void OnReload(ReloadEventArgs args)
        {
            ReadConfig(filepath, Config.NewConfig(), out config);
        }
        private static void ReadConfig<TConfig>(string path, TConfig defaultConfig, out TConfig config)
        {
            if (!File.Exists(path))
            {
                config = defaultConfig;
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            else
            {
                config = JsonConvert.DeserializeObject<TConfig>(File.ReadAllText(path));
            }
        }
        private void OnPlayerUpdateHotel(object sender, GetDataHandlers.PlayerUpdateEventArgs args)
        {
            if(args.Player.CurrentRegion != null)
            {
                Room vRoom = null;
                foreach(var rg in TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY))
                {
                    if(config.Rooms.Exists(x => x.DoorName == rg))
                    {
                        vRoom = config.Rooms.Find(x => x.DoorName == rg);
                        break;
                    }
                }
                if (vRoom != null)
                { 
                    if (args.Player.TPlayer.controlUp)
                    {
                        if (args.Player.Name == vRoom.owner || vRoom.owner == "all")
                        {
                            args.Player.Teleport(vRoom.RoomX * 16, (vRoom.RoomY - 2) * 16);
                            if(vRoom.owner != "all")
                                args.Player.SendInfoMessage("[Hotel] To leave room press \"S\"");
                        }
                        else
                        {
                            args.Player.SendInfoMessage("[Hotel] It's not your room!");
                        }
                    }
                        return;
                }
                foreach (var rg in TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY))
                {
                    if (config.Rooms.Exists(x => x.RoomName == rg))
                    {
                        vRoom = config.Rooms.Find(x => x.RoomName == rg);
                        break;
                    }
                }
                if(vRoom != null)
                    if (args.Player.TPlayer.controlDown)
                    {
                        args.Player.Teleport(vRoom.DoorX * 16, (vRoom.DoorY - 2) * 16);
                        if(vRoom.owner != "all")
                        args.Player.SendInfoMessage("[Hotel] You leave room");
                    }
            }
        }
        private void HotelAdmin(CommandArgs args)
        {
            var p = args.Player;
            if(args.Parameters.Count == 0)
            {
                p.SendInfoMessage("[Hotel] /room add (doorname) (roomname) - Create new room. Set door/room points before adding room");
                p.SendInfoMessage("[Hotel] /room del (roomname) - Delete room");
                p.SendInfoMessage("[Hotel] /room set 1/2 - Set teleport points for door(1) and room(2)");
                p.SendInfoMessage("[Hotel] /room owner (roomname) (player's name) - Set owner for this room (use \"all\" for free access)");
                return;
            }
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(filepath));
            if (args.Parameters[0] == "add")
            {
                if(args.Parameters.Count != 3)
                {
                    p.SendInfoMessage("[Hotel] /room add (doorname) (roomname)");
                    return;
                }
                if (!args.Player.TempPoints.Any(i => i == Point.Zero))
                {
                    Room vRoom = new Room
                    {
                        DoorName = args.Parameters[1],
                        RoomName = args.Parameters[2],
                        DoorX = p.TempPoints[0].X,
                        DoorY = p.TempPoints[0].Y,
                        RoomX = p.TempPoints[1].X,
                        RoomY = p.TempPoints[1].Y
                    };

                    args.Player.TempPoints[0] = Point.Zero;
                    args.Player.TempPoints[1] = Point.Zero;
                    config.Rooms.Add(vRoom);
                    File.WriteAllText(filepath, JsonConvert.SerializeObject(config, Formatting.Indented));
                    p.SendInfoMessage("[Hotel] Room created!");
                }
                else
                {
                    args.Player.SendErrorMessage("[Hotel] Set points first");
                }
            }
            if(args.Parameters[0] == "set")
            {
                int choice = 0;
                if (args.Parameters.Count == 2 &&
                    int.TryParse(args.Parameters[1], out choice) &&
                    choice >= 1 && choice <= 2)
                {
                    args.Player.SendInfoMessage("Hit a block to Set Point " + choice);
                    args.Player.AwaitingTempPoint = choice;
                }
                else
                {
                    args.Player.SendErrorMessage("[Hotel] /room set 1/2 - Set teleport points for door(1) and room(2)");
                }
            }
            if (args.Parameters[0] == "del")
            {
                Room vRoom = null;
                if(args.Parameters.Count != 2)
                {
                    p.SendInfoMessage("[Hotel] /room del (roomname)");
                    return;
                }
                for(int i = 0; i <= config.Rooms.Count - 1; i++)
                {
                    if(config.Rooms[i].RoomName == args.Parameters[1])
                    {
                        config.Rooms.RemoveAt(i);
                        File.WriteAllText(filepath, JsonConvert.SerializeObject(config, Formatting.Indented));
                        p.SendInfoMessage("[Hotel] Room deleted!");
                        return;
                    }
                }
                if (vRoom == null)
                    p.SendInfoMessage("[Hotel] Room not found!");
            }
            if(args.Parameters[0] == "owner")
            {
                if (args.Parameters.Count != 3)
                {
                    p.SendInfoMessage("[Hotel] /room owner (roomname) (player's name) - Set owner of this room for 1 day(use \"all\" for free access forever)");
                    return;
                }
                foreach(var r in config.Rooms)
                {
                    if(r.RoomName == args.Parameters[1])
                    {
                        r.owner = args.Parameters[2];
                        r.RentTime = DateTime.UtcNow.AddDays(1);
                        p.SendInfoMessage("[Hotel] {0} become owner of {1} room for {2}", args.Parameters[1], args.Parameters[2], r.RentTime);
                        File.WriteAllText(filepath, JsonConvert.SerializeObject(config, Formatting.Indented));
                        return;
                    }
                }

            }
        }
        private void HotelMain(CommandArgs args)
        {
            var p = args.Player;
            if (!TShock.Regions.InAreaRegionName(p.TileX, p.TileY).Contains("Hotel"))
            {
                p.SendInfoMessage("You're not in the hotel territory!");
                return;
            }

            if(args.Parameters.Count == 0)
            {
                p.SendInfoMessage("[Hotel] /hotel rent - rent room for 1 day");
                p.SendInfoMessage("[Hotel] /hotel info - price and amount of room info");
                return;
            }
            if(args.Parameters[0] == "rent")
            {
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(filepath));
                Room vRoom = null;
                foreach (var r in config.Rooms)
                {
                    if (r.owner == p.Name)
                    {
                        vRoom = r;
                        break;
                    }
                }
                if(vRoom != null)
                {
                    var bal = SEconomyPlugin.Instance.GetBankAccount(p);
                    if (bal == null || bal.IsAccountEnabled == false ||
                        !Money.TryParse(Convert.ToString(config.Price), out var price))
                    {
                        p.SendErrorMessage("[Hotel] Something went wrong");
                        return;
                    }
                    if (bal.Balance < price)
                    {
                        p.SendInfoMessage("[Hotel] Haven't anough {0}!", Money.CurrencyName);
                        return;
                    }
                    vRoom.RentTime = vRoom.RentTime.AddDays(1);
                    bal.TransferTo(SEconomyPlugin.Instance.WorldAccount, price, BankAccountTransferOptions.AnnounceToSender | BankAccountTransferOptions.IsPayment,
                                    "rent", "rentroom");
                    p.SendInfoMessage("[Hotel] You extend your renttime for {0}", vRoom.RentTime);
                    File.WriteAllText(filepath, JsonConvert.SerializeObject(config, Formatting.Indented));
                    return;
                }
                foreach (var r in config.Rooms)
                {
                    if(r.owner == "none")
                    {
                        vRoom = r;
                        break;
                    }
                }
                if(vRoom == null)
                {
                    p.SendInfoMessage("[Hotel] No free rooms yet.");
                    return;
                }
                else
                {
                    var bal = SEconomyPlugin.Instance.GetBankAccount(p);
                    if (bal == null || bal.IsAccountEnabled == false ||
                        !Money.TryParse(Convert.ToString(config.Price), out var price))
                    {
                        p.SendErrorMessage("[Hotel] Something went wrong");
                        return;
                    }
                    if (bal.Balance < price)
                    {
                        p.SendInfoMessage("[Hotel] Haven't anough {0}!", Money.CurrencyName);
                        return;
                    }
                    bal.TransferTo(SEconomyPlugin.Instance.WorldAccount, price, BankAccountTransferOptions.AnnounceToSender | BankAccountTransferOptions.IsPayment,
                                    "rent", "rentroom");
                    vRoom.owner = args.Player.Name;
                    vRoom.RentTime = DateTime.UtcNow.AddDays(1);
                    p.SendInfoMessage("[Hotel] You rented room {0}! Your rent end for {1}.", vRoom.RoomName, vRoom.RentTime);
                    p.SendInfoMessage("[Hotel] To get in your room go to the door");
                    p.SendInfoMessage("[Hotel] and press W!");
                    File.WriteAllText(filepath, JsonConvert.SerializeObject(config, Formatting.Indented));
                    return;
                }
            }
            if(args.Parameters[0] == "info")
            {
                int i = 0;
                Room vRoom = null;
                foreach (var r in config.Rooms)
                {
                    if(r.owner == p.Name)
                    {
                        vRoom = r;
                        break;
                    }
                    if (r.owner == "none")
                    {
                        i++;
                    }
                }
                p.SendInfoMessage("[Hotel] Room price {0} per day", Money.Parse(Convert.ToString(config.Price)));
                if (vRoom != null)
                    p.SendInfoMessage("[Hotel] Your rent end at {0}", vRoom.RentTime);
                else
                    p.SendInfoMessage("[Hotel] Free rooms: {0}", i);
                return;
            }
            p.SendInfoMessage("[Hotel] /hotel rent - rent room for 1 day");
            p.SendInfoMessage("[Hotel] /hotel info - price and amount of room info");
        }
    }
}