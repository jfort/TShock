﻿/*
TShock, a server mod for Terraria
Copyright (C) 2011-2013 Nyx Studios (fka. The TShock Team)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Terraria;
using TShockAPI.DB;

namespace TShockAPI
{
	public delegate void CommandDelegate(CommandArgs args);

	public class CommandArgs : EventArgs
	{
		public string Message { get; private set; }
		public TSPlayer Player { get; private set; }

		/// <summary>
		/// Parameters passed to the arguement. Does not include the command name.
		/// IE '/kick "jerk face"' will only have 1 argument
		/// </summary>
		public List<string> Parameters { get; private set; }

		public Player TPlayer
		{
			get { return Player.TPlayer; }
		}

		public CommandArgs(string message, TSPlayer ply, List<string> args)
		{
			Message = message;
			Player = ply;
			Parameters = args;
		}
	}

	public class Command
	{
		/// <summary>
		/// Gets or sets whether to allow non-players to use this command.
		/// </summary>
		public bool AllowServer { get; set; }
		/// <summary>
		/// Gets or sets whether to do logging of this command.
		/// </summary>
		public bool DoLog { get; set; }
		/// <summary>
		/// Gets or sets the help text of this command.
		/// </summary>
		public string HelpText { get; set; }
		/// <summary>
		/// Gets the name of the command.
		/// </summary>
		public string Name { get { return Names[0]; } }
		/// <summary>
		/// Gets the names of the command.
		/// </summary>
		public List<string> Names { get; protected set; }
		/// <summary>
		/// Gets the permissions of the command.
		/// </summary>
		public List<string> Permissions { get; protected set; }

		private CommandDelegate commandDelegate;
		public CommandDelegate CommandDelegate
		{
			get { return commandDelegate; }
			set
			{
				if (value == null)
					throw new ArgumentNullException();

				commandDelegate = value;
			}
	 	}

		public Command(List<string> permissions, CommandDelegate cmd, params string[] names)
			: this(cmd, names)
		{
			Permissions = permissions;
		}

		public Command(string permissions, CommandDelegate cmd, params string[] names)
			: this(cmd, names)
		{
			Permissions = new List<string> { permissions };
		}

		public Command(CommandDelegate cmd, params string[] names)
		{
			if (cmd == null)
				throw new ArgumentNullException("cmd");
			if (names == null || names.Length < 1)
				throw new ArgumentException("names");

			AllowServer = true;
			CommandDelegate = cmd;
			DoLog = true;
			HelpText = "Napoveda neni k dispozici.";
			Names = new List<string>(names);
			Permissions = new List<string>();
		}

		public bool Run(string msg, TSPlayer ply, List<string> parms)
		{
			if (!CanRun(ply))
				return false;

			try
			{
				CommandDelegate(new CommandArgs(msg, ply, parms));
			}
			catch (Exception e)
			{
				ply.SendErrorMessage("Chybny prikaz. Podivej se do logu pro vice podrobnosti o chybe.");
				Log.Error(e.ToString());
			}

			return true;
		}

		public bool HasAlias(string name)
		{
			return Names.Contains(name);
		}

		public bool CanRun(TSPlayer ply)
		{
			if (Permissions == null || Permissions.Count < 1)
				return true;
			foreach (var Permission in Permissions)
			{
				if (ply.Group.HasPermission(Permission))
					return true;
			}
			return false;
		}
	}

	public static class Commands
	{
		public static List<Command> ChatCommands = new List<Command>();
		public static ReadOnlyCollection<Command> TShockCommands = new ReadOnlyCollection<Command>(new List<Command>());

		private delegate void AddChatCommand(string permission, CommandDelegate command, params string[] names);

		public static void InitCommands()
		{
			List<Command> tshockCommands = new List<Command>(100);
			Action<Command> add = (cmd) => 
			{
				tshockCommands.Add(cmd);
				ChatCommands.Add(cmd);
			};

			add(new Command(AuthToken, "auth")
			{
				AllowServer = false,
				HelpText = "Used to authenticate as superadmin when first setting up TShock."
			});
			add(new Command(Permissions.authverify, AuthVerify, "auth-verify")
			{
				HelpText = "Used to verify that you have correctly set up TShock."
			});
			add(new Command(Permissions.user, ManageUsers, "user")
			{
				DoLog = false,
				HelpText = "Manages user accounts."
			});

			#region Account Commands
			add(new Command(Permissions.canlogin, AttemptLogin, "login")
			{
				AllowServer = false,
				DoLog = false,
				HelpText = "Slouzi k prihlaseni na tvuj ucet."
			});
			add(new Command(Permissions.canchangepassword, PasswordUser, "password")
			{
				AllowServer = false,
				DoLog = false,
				HelpText = "Slouzi ke zmene tveho hesla."
			});
			add(new Command(Permissions.canregister, RegisterUser, "register")
			{
				AllowServer = false,
				DoLog = false,
				HelpText = "Slouzi k registrovani noveho uctu hrace na tomto serveru."
			});
            add(new Command(Permissions.potvrzeniregistrace, potvrzeniregistrace, "potvrdit")
            {
                AllowServer = false,
                DoLog = false,
                HelpText = "Prikaz potvrdi registraci uvedeneho hrace."
            });
            add(new Command(Permissions.rucniregistrace, rucniregistrace, "registruj")
            {
                AllowServer = false,
                DoLog = false,
                HelpText = "Provede rucni registraci hrace dle zadanych informaci."
            });


			#endregion
			#region Admin Commands
			add(new Command(Permissions.ban, Ban, "ban")
			{
				HelpText = "Manages player bans."
			});
			add(new Command(Permissions.broadcast, Broadcast, "broadcast", "bc", "say")
			{
				HelpText = "Broadcasts a message to everyone on the server."
			});
			add(new Command(Permissions.logs, DisplayLogs, "displaylogs")
			{
				HelpText = "Toggles whether you receive server logs."
			});
			add(new Command(Permissions.managegroup, Group, "group")
			{
				HelpText = "Manages groups."
			});
			add(new Command(Permissions.manageitem, ItemBan, "itemban")
			{
				HelpText = "Manages item bans."
			});
			add(new Command(Permissions.manageregion, Region, "region")
			{
				HelpText = "Manages regions."
			});
			add(new Command(Permissions.kick, Kick, "kick")
			{
				HelpText = "Removes a player from the server."
			});
			add(new Command(Permissions.mute, Mute, "mute", "unmute")
			{
				HelpText = "Prevents a player from talking."
			});
			add(new Command(Permissions.savessc, OverrideSSC, "overridessc", "ossc")
			{
				HelpText = "Overrides serverside characters for a player, temporarily."
			});
			add(new Command(Permissions.savessc, SaveSSC, "savessc")
			{
				HelpText = "Saves all serverside characters."
			});
			add(new Command(Permissions.settempgroup, TempGroup, "tempgroup")
			{
				HelpText = "Temporarily sets another player's group."
			});
			add(new Command(Permissions.userinfo, GrabUserUserInfo, "userinfo", "ui")
			{
				HelpText = "Shows information about a user."
			});
			#endregion
			#region Annoy Commands
			add(new Command(Permissions.annoy, Annoy, "annoy")
			{
				HelpText = "Annoys a player for an amount of time."
			});
			add(new Command(Permissions.annoy, Confuse, "confuse")
			{
				HelpText = "Confuses a player for an amount of time."
			});
			add(new Command(Permissions.annoy, Rocket, "rocket")
			{
				HelpText = "Rockets a player upwards. Requires SSC."
			});
			add(new Command(Permissions.annoy, FireWork, "firework")
			{
				HelpText = "Spawns fireworks at a player."
			});
			#endregion

			#region Configuration Commands
			add(new Command(Permissions.maintenance, CheckUpdates, "checkupdates")
			{
				HelpText = "Checks for TShock updates."
			});
			add(new Command(Permissions.maintenance, Off, "off", "exit")
			{
				HelpText = "Shuts down the server while saving."
			});
			add(new Command(Permissions.maintenance, OffNoSave, "off-nosave", "exit-nosave")
			{
				HelpText = "Shuts down the server without saving."
			});
			add(new Command(Permissions.maintenance, Reload, "reload")
			{
				HelpText = "Reloads the server configuration file."
			});
			add(new Command(Permissions.maintenance, Restart, "restart")
			{
				HelpText = "Restarts the server."
			});
			add(new Command(Permissions.cfgpassword, ServerPassword, "serverpassword")
			{
				HelpText = "Changes the server password."
			});
			add(new Command(Permissions.verze, GetVersion, "version")
			{
				HelpText = "Shows the TShock version."
			});
			/* Does nothing atm.
			 * 
			 * add(new Command(Permissions.updateplugins, UpdatePlugins, "updateplugins")
			{
			});*/
			add(new Command(Permissions.whitelist, Whitelist, "whitelist")
			{
				HelpText = "Manages the server whitelist."
			});
			#endregion

			#region Item Commands
			add(new Command(Permissions.item, Give, "give", "g")
			{
				HelpText = "Gives another player an item."
			});
			add(new Command(Permissions.item, Item, "item", "i")
			{
				AllowServer = false,
				HelpText = "Gives yourself an item."
			});
			#endregion

			#region NPC Commands
			add(new Command(Permissions.butcher, Butcher, "butcher")
			{
				HelpText = "Kills hostile NPCs or NPCs of a certain type."
			});
			add(new Command(Permissions.invade, Invade, "invade")
			{
				HelpText = "Starts an NPC invasion."
			});
			add(new Command(Permissions.maxspawns, MaxSpawns, "maxspawns")
			{
				HelpText = "Sets the maximum number of NPCs."
			});
			add(new Command(Permissions.spawnboss, SpawnBoss, "spawnboss", "sb")
			{
				AllowServer = false,
				HelpText = "Spawns a number of bosses around you."
			});
			add(new Command(Permissions.spawnmob, SpawnMob, "spawnmob", "sm")
			{
				AllowServer = false,
				HelpText = "Spawns a number of mobs around you."
			});
			add(new Command(Permissions.spawnrate, SpawnRate, "spawnrate")
			{
				HelpText = "Sets the spawn rate of NPCs."
			});
			add(new Command(Permissions.invade, PumpkinInvasion, "pumpkin")
			{
				HelpText = "Starts a Pumpkin Moon invasion at the specified wave."
			});
			#endregion
			#region TP Commands
			add(new Command(Permissions.home, Home, "home")
			{
				AllowServer = false,
				HelpText = "Sends you to your spawn point."
			});
			add(new Command(Permissions.spawn, Spawn, "spawn")
			{
				AllowServer = false,
				HelpText = "Sends you to the world's spawn point."
			});
			add(new Command(Permissions.tp, TP, "tp")
			{
				AllowServer = false,
				HelpText = "Teleports you to another player or a coordinate."
			});
			add(new Command(Permissions.tpallow, TPAllow, "tpallow")
			{
				AllowServer = false,
				HelpText = "Toggles whether other people can teleport to you."
			});
			add(new Command(Permissions.tphere, TPHere, "tphere")
			{
				AllowServer = false,
				HelpText = "Teleports another player to you."
			});
			#endregion
			#region World Commands
			add(new Command(Permissions.antibuild, ToggleAntiBuild, "antibuild")
			{
				HelpText = "Toggles build protection."
			});
			add(new Command(Permissions.bloodmoon, Bloodmoon, "bloodmoon")
			{
				HelpText = "Sets a blood moon."
			});
			add(new Command(Permissions.grow, Grow, "grow")
			{
				AllowServer = false,
				HelpText = "Grows plants at your location."
			});
			add(new Command(Permissions.dropmeteor, DropMeteor, "dropmeteor")
			{
				HelpText = "Drops a meteor somewhere in the world."
			});
			add(new Command(Permissions.eclipse, Eclipse, "eclipse")
			{
				HelpText = "Sets an eclipse."
			});
			add(new Command(Permissions.xmas, ForceXmas, "forcexmas")
			{
				HelpText = "Toggles christmas mode (present spawning, santa, etc)."
			});
			add(new Command(Permissions.fullmoon, Fullmoon, "fullmoon")
			{
				HelpText = "Sets a full moon."
			});
			add(new Command(Permissions.hardmode, Hardmode, "hardmode")
			{
				HelpText = "Toggles the world's hardmode status."
			});
			add(new Command(Permissions.editspawn, ProtectSpawn, "protectspawn")
			{
				HelpText = "Toggles spawn protection."
			});
			add(new Command(Permissions.rain, Rain, "rain")
			{
				HelpText = "Toggles the rain."
			});
			add(new Command(Permissions.worldsave, Save, "save")
			{
				HelpText = "Saves the world file."
			});
			add(new Command(Permissions.worldspawn, SetSpawn, "setspawn")
			{
				AllowServer = false,
				HelpText = "Sets the world's spawn point to your location."
			});
			add(new Command(Permissions.worldsettle, Settle, "settle")
			{
				HelpText = "Forces all liquids to update immediately."
			});
			add(new Command(Permissions.time, Time, "time")
			{
				HelpText = "Sets the world time."
			});
			add(new Command(Permissions.worldinfo, WorldInfo, "world")
			{
				HelpText = "Shows information about the current world."
			});
			#endregion
			#region Other Commands
			add(new Command(Permissions.buff, Buff, "buff")
			{
				AllowServer = false,
				HelpText = "Gives yourself a buff for an amount of time."
			});
			add(new Command(Permissions.clear, Clear, "clear")
			{
				HelpText = "Clears item drops or projectiles."
			});
			add(new Command(Permissions.buffplayer, GBuff, "gbuff", "buffplayer")
			{
				HelpText = "Gives another player a buff for an amount of time."
			});
			add(new Command(Permissions.godmode, ToggleGodMode, "godmode")
			{
				HelpText = "Toggles godmode on a player."
			});
			add(new Command(Permissions.heal, Heal, "heal")
			{
				HelpText = "Heals a player in HP and MP."
			});
			add(new Command(Permissions.kill, Kill, "kill")
			{
				HelpText = "Kills another player."
			});
			add(new Command(Permissions.cantalkinthird, ThirdPerson, "me")
			{
				HelpText = "Sends an action message to everyone."
			});
			add(new Command(Permissions.canpartychat, PartyChat, "party", "p")
			{
				AllowServer = false,
				HelpText = "Sends a message to everyone on your team."
			});
			add(new Command(Permissions.whisper, Reply, "reply", "r")
			{
				HelpText = "Replies to a PM sent to you."
			});
			add(new Command(Rests.RestPermissions.restmanage, ManageRest, "rest")
			{
				HelpText = "Manages the REST API."
			});
			add(new Command(Permissions.slap, Slap, "slap")
			{
				HelpText = "Slaps a player, dealing damage."
			});
			add(new Command(Permissions.serverinfo, ServerInfo, "stats")
			{
				HelpText = "Shows the server information."
			});
			add(new Command(Permissions.warp, Warp, "warp")
			{
				HelpText = "Teleports you to a warp point or manages warps."
			});
			add(new Command(Permissions.whisper, Whisper, "whisper", "w", "tell")
			{
				HelpText = "Sends a PM to a player."
			});
			#endregion

			add(new Command(Aliases, "aliases")
			{
				HelpText = "Shows a command's aliases."
			});
			add(new Command(Help, "help")
			{
				HelpText = "Zobrazi napovedu k prikazum, ktere mas pristupne."
			});
			add(new Command(Motd, "motd")
			{
				HelpText = "Zobrazi text zpravy dne."
			});
			add(new Command(ListConnectedPlayers, "playing", "online", "who")
			{
				HelpText = "Zobrazi aktualne pripojene hrace."
			});
			add(new Command(Rules, "rules")
			{
				HelpText = "Zobrazi pravidla serveru. Kompletni zneni pravidel je na webu!"
			});

			TShockCommands = new ReadOnlyCollection<Command>(tshockCommands);
		}

		public static bool HandleCommand(TSPlayer player, string text)
		{
			string cmdText = text.Remove(0, 1);

			var args = ParseParameters(cmdText);
			if (args.Count < 1)
				return false;

			string cmdName = args[0].ToLower();
			args.RemoveAt(0);

			if (Hooks.PlayerHooks.OnPlayerCommand(player, cmdName, cmdText, args))
				return true;

			IEnumerable<Command> cmds = ChatCommands.Where(c => c.HasAlias(cmdName));

			if (cmds.Count() == 0)
			{
				if (player.AwaitingResponse.ContainsKey(cmdName))
				{
					Action<CommandArgs> call = player.AwaitingResponse[cmdName];
					player.AwaitingResponse.Remove(cmdName);
					call(new CommandArgs(cmdText, player, args));
					return true;
				}
				player.SendErrorMessage("Zadan chybny prikaz. Napis /help pro vypis platnych prikazu.");
				return true;
			}
            foreach (Command cmd in cmds)
            {
                if (!cmd.CanRun(player))
                {
                    TShock.Utils.SendLogs(string.Format("{0} tried to execute /{1}.", player.Name, cmdText), Color.PaleVioletRed, player);
                    player.SendErrorMessage("Pro tento prikaz nemas potrebna opravneni.");
                }
                else if (!cmd.AllowServer && !player.RealPlayer)
                {
                    player.SendErrorMessage("K provedeni tohoto prikazu musis byt ve hre.");
                }
                else
                {
                    if (cmd.DoLog)
                        TShock.Utils.SendLogs(string.Format("{0} executed: /{1}.", player.Name, cmdText), Color.PaleVioletRed, player);
                    cmd.Run(cmdText, player, args);
                }
            }
		    return true;
		}

		/// <summary>
		/// Parses a string of parameters into a list. Handles quotes.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		private static List<String> ParseParameters(string str)
		{
			var ret = new List<string>();
			var sb = new StringBuilder();
			bool instr = false;
			for (int i = 0; i < str.Length; i++)
			{
				char c = str[i];

				if (instr)
				{
					if (c == '\\')
					{
						if (i + 1 >= str.Length)
							break;
						c = GetEscape(str[++i]);
					}
					else if (c == '"')
					{
						ret.Add(sb.ToString());
						sb.Clear();
						instr = false;
						continue;
					}
					sb.Append(c);
				}
				else
				{
					if (IsWhiteSpace(c))
					{
						if (sb.Length > 0)
						{
							ret.Add(sb.ToString());
							sb.Clear();
						}
					}
					else if (c == '"')
					{
						if (sb.Length > 0)
						{
							ret.Add(sb.ToString());
							sb.Clear();
						}
						instr = true;
					}
					else
					{
						sb.Append(c);
					}
				}
			}
			if (sb.Length > 0)
				ret.Add(sb.ToString());

			return ret;
		}

		private static char GetEscape(char c)
		{
			switch (c)
			{
				case '\\':
					return '\\';
				case '"':
					return '"';
				case 't':
					return '\t';
				default:
					return c;
			}
		}

		private static bool IsWhiteSpace(char c)
		{
			return c == ' ' || c == '\t' || c == '\n';
		}

		#region Account commands

		private static void AttemptLogin(CommandArgs args)
		{
			if (args.Player.LoginAttempts > TShock.Config.MaximumLoginAttempts && (TShock.Config.MaximumLoginAttempts != -1))
			{
				Log.Warn(String.Format("{0} ({1}) provedl {2} nebo vice chybnych pokusu o prihlaseni abyl automaticky vykopnut.",
					args.Player.IP, args.Player.Name, TShock.Config.MaximumLoginAttempts));
				TShock.Utils.Kick(args.Player, "Prilis mnoho chybnych pokusu o prihlaseni.");
				return;
			}
            
			User user = TShock.Users.GetUserByName(args.Player.Name);
			string encrPass = "";
			bool usingUUID = false;
			if (args.Parameters.Count == 0 && !TShock.Config.DisableUUIDLogin)
			{
				if (Hooks.PlayerHooks.OnPlayerPreLogin(args.Player, args.Player.Name, ""))
					return;
				user = TShock.Users.GetUserByName(args.Player.Name);
				usingUUID = true;
			}
			else if (args.Parameters.Count == 1)
			{
				if (Hooks.PlayerHooks.OnPlayerPreLogin(args.Player, args.Player.Name, args.Parameters[0]))
					return;
				user = TShock.Users.GetUserByName(args.Player.Name);
				encrPass = TShock.Utils.HashPassword(args.Parameters[0]);
			}
			else if (args.Parameters.Count == 2 && TShock.Config.AllowLoginAnyUsername)
			{
				if (Hooks.PlayerHooks.OnPlayerPreLogin(args.Player, args.Parameters[0], args.Parameters[1]))
					return;

				user = TShock.Users.GetUserByName(args.Parameters[0]);
				encrPass = TShock.Utils.HashPassword(args.Parameters[1]);
				if (String.IsNullOrEmpty(args.Parameters[0]))
				{
					args.Player.SendErrorMessage("Chybne prihlaseni.");
					return;
				}
			}
			else
			{
				args.Player.SendErrorMessage("Spravne zadani: /login - provede aut. prihlaseni pomoci tveho jmena");
				args.Player.SendErrorMessage("        /login <heslo> - provede prihlaseni dle zadaneho hesla a jmena tve akt. postavy");
				args.Player.SendErrorMessage("        /login <jmeno> <heslo> - provede prihlaseni dle zadanych udaju");
				args.Player.SendErrorMessage("Jestlize zapomenes sve heslo, nemuzes jej nijak obnovit. Pozadej pripadne adminy.");
				return;
			}
			try
			{
				if (user == null)
				{
					args.Player.SendErrorMessage("Uzivatel s timto jmenem neexistuje.");
				}
				else if (user.Password.ToUpper() == encrPass.ToUpper() ||
						(usingUUID && user.UUID == args.Player.UUID && !TShock.Config.DisableUUIDLogin &&
						!String.IsNullOrWhiteSpace(args.Player.UUID)))
				{
					args.Player.PlayerData = TShock.CharacterDB.GetPlayerData(args.Player, TShock.Users.GetUserID(user.Name));

					var group = TShock.Utils.GetGroup(user.Group);

					if (TShock.Config.ServerSideCharacter)
					{
						if (group.HasPermission(Permissions.bypassinventorychecks))
						{
							args.Player.IgnoreActionsForClearingTrashCan = false;
						}
						args.Player.PlayerData.RestoreCharacter(args.Player);
					}
					args.Player.LoginFailsBySsi = false;

					if (group.HasPermission(Permissions.ignorestackhackdetection))
						args.Player.IgnoreActionsForCheating = "none";

					if (group.HasPermission(Permissions.usebanneditem))
						args.Player.IgnoreActionsForDisabledArmor = "none";

					args.Player.Group = group;
					args.Player.tempGroup = null;
					args.Player.UserAccountName = user.Name;
					args.Player.UserID = TShock.Users.GetUserID(args.Player.UserAccountName);
					args.Player.IsLoggedIn = true;
					args.Player.IgnoreActionsForInventory = "none";

					if (!args.Player.IgnoreActionsForClearingTrashCan && TShock.Config.ServerSideCharacter)
					{
						args.Player.PlayerData.CopyCharacter(args.Player);
						TShock.CharacterDB.InsertPlayerData(args.Player);
					}
					args.Player.SendSuccessMessage("Uspesne jsi se overil jako " + user.Name + ".");

					Log.ConsoleInfo("Hrac " + args.Player.Name + " se uspesne overil uctem: " + user.Name + ".");
					if ((args.Player.LoginHarassed) && (TShock.Config.RememberLeavePos))
					{
						if (TShock.RememberedPos.GetLeavePos(args.Player.Name, args.Player.IP) != Vector2.Zero)
						{
							Vector2 pos = TShock.RememberedPos.GetLeavePos(args.Player.Name, args.Player.IP);
							args.Player.Teleport((int) pos.X*16, (int) pos.Y*16);
						}
						args.Player.LoginHarassed = false;

					}
					TShock.Users.SetUserUUID(user, args.Player.UUID);

					Hooks.PlayerHooks.OnPlayerPostLogin(args.Player);
				}
				else
				{
					if (usingUUID && !TShock.Config.DisableUUIDLogin)
					{
						args.Player.SendErrorMessage("UUID neodpovida teto postave!");
					}
					else
					{
						args.Player.SendErrorMessage("chybne heslo!");
					}
					Log.Warn("Hrac " + args.Player.IP + " se chybne overil - ucet: " + user.Name + ".");
					args.Player.LoginAttempts++;
				}
			}
			catch (Exception ex)
			{
				args.Player.SendErrorMessage("Ups. Pri zpracovani prikazu doslo k chybe.");
				Log.Error(ex.ToString());
			}
		}

		private static void PasswordUser(CommandArgs args)
		{
			try
			{
				if (args.Player.IsLoggedIn && args.Parameters.Count == 2)
				{
					var user = TShock.Users.GetUserByName(args.Player.UserAccountName);
					string encrPass = TShock.Utils.HashPassword(args.Parameters[0]);
					if (user.Password.ToUpper() == encrPass.ToUpper())
					{
						args.Player.SendSuccessMessage("Upsesne jsi zmenil sve heslo!");
						TShock.Users.SetUserPassword(user, args.Parameters[1]); // SetUserPassword will hash it for you.
						Log.ConsoleInfo(args.Player.IP + " named " + args.Player.Name + " changed the password of account " + user.Name + ".");
					}
					else
					{
						args.Player.SendErrorMessage("Zmena hesla se nezdarila!");
						Log.ConsoleError(args.Player.IP + " named " + args.Player.Name + " failed to change password for account: " +
										 user.Name + ".");
					}
				}
				else
				{
					args.Player.SendErrorMessage("Nejsi prihlasen nebo byl prikaz zadan chybne! Spravne zadani: /password <stare heslo> <nove heslo>");
				}
			}
			catch (UserManagerException ex)
			{
				args.Player.SendErrorMessage("Promin, doslo k nejake necekane chybe: " + ex.Message + ".");
				Log.ConsoleError("PasswordUser returned an error: " + ex);
			}
		}

		private static void RegisterUser(CommandArgs args)
		{
			try
			{
				var user = new User();

				if (args.Parameters.Count == 1)
				{
					user.Name = args.Player.Name;
					user.Password = args.Parameters[0];
                    user.RegisterIp = args.Player.IP;
				}
				else if (args.Parameters.Count == 2 && TShock.Config.AllowRegisterAnyUsername)
				{
					user.Name = args.Parameters[0];
					user.Password = args.Parameters[1];
                    user.RegisterIp = args.Player.IP;
				}
				else
				{
					args.Player.SendErrorMessage("Chybne zadani! Sprave zadani prikazu: /register <heslo>");
					return;
				}

				user.Group = TShock.Config.DefaultRegistrationGroupName; // FIXME -- we should get this from the DB. --Why?
				user.UUID = args.Player.UUID;
                args.Player.SendSuccessMessage("IP: " + user.RegisterIp.ToString());

                if (TShock.Users.GetUserByName(user.Name) == null && user.Name != TSServerPlayer.AccountName) // Cheap way of checking for existance of a user
				if  (TShock.Users.GetUserByRegisteredIP(user.RegisterIp.ToString()) == null) 
                {
                    args.Player.SendSuccessMessage("Ucet \"{0}\" byl uspesne registrovan. Znova se prihlas a muzes hrat.", user.Name);
					args.Player.SendSuccessMessage("Tve heslo je {0}.", user.Password);
					TShock.Users.AddUser(user); 
					TShock.CharacterDB.SeedInitialData(TShock.Users.GetUser(user));
					Log.ConsoleInfo("{0} uspesne registroval ucet: \"{1}\".", args.Player.Name, user.Name);
				}
				else
				{
					args.Player.SendErrorMessage("Ucet na toto jmeno nebo tvoji IP adresu je jiz registrovan.");
					Log.ConsoleInfo(args.Player.Name + " se pokusil registrovat ucet na jiz existujici jmeno nebo IP adresu");
				}
			}
			catch (UserManagerException ex)
			{
                args.Player.SendErrorMessage("Promin, doslo k nejake necekane chybe: " + ex.Message + ".");
                Log.ConsoleError("PasswordUser returned an error: " + ex); ;
			}
		}

        private static void potvrzeniregistrace(CommandArgs args)
        {
            // This guy needs to be here so that people don't get exceptions when they type /user
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Chybne zadany prikaz. Zadej: /potvrdit <jmenohrace>");
                return;
            }

            string hrac = args.Parameters[0];
            var user = new User();
            user = TShock.Users.GetUserByName(hrac);

            try
            {
                if (user.Group == "default")
                {
                    TShock.Users.SetUserGroup(user, "registered");
                    args.Player.SendSuccessMessage("Registrace hrace " + user.Name + " byla potvrzena!");
                    Log.ConsoleInfo("Registrace hrace " + user.Name + " byla potvrzena!");
                }
                else
                {
                    args.Player.SendSuccessMessage("Registrace hrace " + user.Name + " NEBYLA provedena. Je jiz registrovan!");
                    args.Player.SendSuccessMessage("Stavajici skupina:" + args.Player.Group.Name);
                    Log.ConsoleInfo("Registrace hrace " + user.Name + " NEBYLA provedena. Je jiz registrovan!");
                }
            }
            catch (UserManagerException ex)
            {
                args.Player.SendMessage(ex.Message, Color.Green);
                Log.ConsoleError(ex.ToString());
            }
        }

        private static void rucniregistrace(CommandArgs args)
        {


            try
            {
                if (args.Parameters.Count > 0)
                {
                    var namepass = args.Parameters[0].Split(':');
                    var user = new User();

                    if (namepass.Length == 2)
                    {
                        user.Name = namepass[0];
                        user.Password = namepass[1];
                        user.Group = "registered";

                        if (TShock.Users.GetUserByName(user.Name) != null)
                        {
                            args.Player.SendErrorMessage("Chyba: hrac s timto jmenem jiz existuje! Nelze registrovat.");
                            return;
                        }

                        args.Player.SendSuccessMessage("Ucet " + user.Name + " byl uspesne registrovan. Prirazena skupina " + user.Group + ".");
                        TShock.Users.AddUser(user);
                        Log.ConsoleInfo(args.Player.Name + " registroval ucet " + user.Name + ". Prirazena skupina " + user.Group + ".");
                    }
                }
                else
                {
                    args.Player.SendErrorMessage("Chybne zadani prikazu. Spravne zadani: /registruj <jmeno>:<heslo>.");
                }
            }
            catch (UserManagerException ex)
            {
                args.Player.SendErrorMessage(ex.Message);
                Log.ConsoleError(ex.ToString());
            }
        }

		private static void ManageUsers(CommandArgs args)
		{
			// This guy needs to be here so that people don't get exceptions when they type /user
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid user syntax. Try /user help.");
				return;
			}

			string subcmd = args.Parameters[0];

			// Add requires a username, password, and a group specified.
			if (subcmd == "add")
			{
				var user = new User();

				try
				{
					if (args.Parameters.Count == 4)
					{
						user.Name = args.Parameters[1];
						user.Password = args.Parameters[2];
						user.Group = args.Parameters[3];
							
                        args.Player.SendSuccessMessage("Account " + user.Name + " has been added to group " + user.Group + "!");
						TShock.Users.AddUser(user);
						TShock.CharacterDB.SeedInitialData(TShock.Users.GetUser(user));
						Log.ConsoleInfo(args.Player.Name + " added Account " + user.Name + " to group " + user.Group);
					}
					else
					{
						args.Player.SendErrorMessage("Invalid syntax. Try /user help.");
					}
				}
				catch (UserManagerException ex)
				{
					args.Player.SendErrorMessage(ex.Message);
					Log.ConsoleError(ex.ToString());
				}
			}
				// User deletion requires a username
			else if (subcmd == "del" && args.Parameters.Count == 2)
			{
				var user = new User();
				user.Name = args.Parameters[1];

				try
				{
					TShock.Users.RemoveUser(user);
					args.Player.SendSuccessMessage("Account removed successfully.");
					Log.ConsoleInfo(args.Player.Name + " successfully deleted account: " + args.Parameters[1] + ".");
				}
				catch (UserManagerException ex)
				{
					args.Player.SendMessage(ex.Message, Color.Red);
					Log.ConsoleError(ex.ToString());
				}
			}
				// Password changing requires a username, and a new password to set
			else if (subcmd == "password")
			{
				var user = new User();
				user.Name = args.Parameters[1];

				try
				{
					if (args.Parameters.Count == 3)
					{
						args.Player.SendSuccessMessage("Password change succeeded for " + user.Name + ".");
						TShock.Users.SetUserPassword(user, args.Parameters[2]);
						Log.ConsoleInfo(args.Player.Name + " changed the password of account " + user.Name);
					}
					else
					{
						args.Player.SendErrorMessage("Invalid user password syntax. Try /user help.");
					}
				}
				catch (UserManagerException ex)
				{
					args.Player.SendErrorMessage(ex.Message);
					Log.ConsoleError(ex.ToString());
				}
			}
				// Group changing requires a username or IP address, and a new group to set
			else if (subcmd == "group")
			{
                var user = new User();
                user.Name = args.Parameters[1];

				try
				{
					if (args.Parameters.Count == 3)
					{
						args.Player.SendSuccessMessage("Account " + user.Name + " has been changed to group " + args.Parameters[2] + "!");
						TShock.Users.SetUserGroup(user, args.Parameters[2]);
						Log.ConsoleInfo(args.Player.Name + " changed account " + user.Name + " to group " + args.Parameters[2] + ".");
					}
					else
					{
						args.Player.SendErrorMessage("Invalid user group syntax. Try /user help.");
					}
				}
				catch (UserManagerException ex)
				{
					args.Player.SendMessage(ex.Message, Color.Green);
					Log.ConsoleError(ex.ToString());
				}
			}
			else if (subcmd == "help")
			{
				args.Player.SendInfoMessage("Use command help:");
				args.Player.SendInfoMessage("/user add username password group   -- Adds a specified user");
				args.Player.SendInfoMessage("/user del username                  -- Removes a specified user");
				args.Player.SendInfoMessage("/user password username newpassword -- Changes a user's password");
				args.Player.SendInfoMessage("/user group username newgroup       -- Changes a user's group");
			}
			else
			{
				args.Player.SendErrorMessage("Invalid user syntax. Try /user help.");
			}
		}

		#endregion

		#region Stupid commands

		private static void ServerInfo(CommandArgs args)
		{
			args.Player.SendInfoMessage("Memory usage: " + Process.GetCurrentProcess().WorkingSet64);
			args.Player.SendInfoMessage("Allocated memory: " + Process.GetCurrentProcess().VirtualMemorySize64);
			args.Player.SendInfoMessage("Total processor time: " + Process.GetCurrentProcess().TotalProcessorTime);
			args.Player.SendInfoMessage("WinVer: " + Environment.OSVersion);
			args.Player.SendInfoMessage("Proc count: " + Environment.ProcessorCount);
			args.Player.SendInfoMessage("Machine name: " + Environment.MachineName);
		}

		private static void WorldInfo(CommandArgs args)
		{
			args.Player.SendInfoMessage("World name: " + Main.worldName);
			args.Player.SendInfoMessage("World size: {0}x{1}", Main.maxTilesX, Main.maxTilesY);
			args.Player.SendInfoMessage("World ID: " + Main.worldID);
		}

		#endregion

		#region Player Management Commands

		private static void GrabUserUserInfo(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /userinfo <player>");
				return;
			}

			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
				return;
			}
			try
			{
				args.Player.SendSuccessMessage("IP Address: " + players[0].IP + " Logged in as: " + players[0].UserAccountName + " group: " + players[0].Group.Name);
			}
			catch (Exception)
			{
				args.Player.SendErrorMessage("Invalid player.");
			}
		}

		private static void Kick(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /kick <player> [reason]");
				return;
			}
			if (args.Parameters[0].Length == 0)
			{
				args.Player.SendErrorMessage("Missing player name.");
				return;
			}

			string plStr = args.Parameters[0];
			var players = TShock.Utils.FindPlayer(plStr);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid player!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else
			{
				string reason = args.Parameters.Count > 1
									? String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1))
									: "Misbehaviour.";
				if (!TShock.Utils.Kick(players[0], reason, !args.Player.RealPlayer, false, args.Player.Name))
				{
					args.Player.SendErrorMessage("You can't kick another admin!");
				}
			}
		}

		private static void Ban(CommandArgs args)
		{
			if (args.Parameters.Count == 0 || args.Parameters[0].ToLower() == "help")
			{
				args.Player.SendInfoMessage("Syntax: /ban [option] [arguments]");
				args.Player.SendInfoMessage("Options: list, listip, clear, add, addip, del, delip");
				args.Player.SendInfoMessage("Arguments: list, listip, clear [code], add [name], addip [ip], del [name], delip [name]");
				args.Player.SendInfoMessage("In addition, a reason may be provided for all new bans after the arguments.");
				return;
			}
			if (args.Parameters[0].ToLower() == "list")
			{
				#region List bans
				if (TShock.Bans.GetBans().Count == 0)
				{
					args.Player.SendErrorMessage("There are currently no players banned.");
					return;
				}

				string banString = "";
				foreach (Ban b in TShock.Bans.GetBans())
				{

					if (b.Name.Trim() == "")
					{
						continue;
					}

					if (banString.Length == 0)
					{
						banString = b.Name;
					}
					else
					{
						int length = banString.Length;
						while (length > 60)
						{
							length = length - 60;
						}
						if (length + b.Name.Length >= 60)
						{
							banString += "|, " + b.Name;
						}
						else
						{
							banString += ", " + b.Name;
						}
					}
				}

				String[] banStrings = banString.Split('|');

				if (banStrings.Length == 0)
				{
					args.Player.SendErrorMessage("There are currently no players with valid names banned.");
					return;
				}

				if (banStrings[0].Trim() == "")
				{
					args.Player.SendErrorMessage("There are currently no bans with valid names found.");
					return;
				}

				args.Player.SendInfoMessage("List of banned players:");
				foreach (string s in banStrings)
				{
					args.Player.SendInfoMessage(s);
				}
				return;
				#endregion List bans
			}

			if (args.Parameters[0].ToLower() == "listip")
			{
				#region List ip bans
				if (TShock.Bans.GetBans().Count == 0)
				{
					args.Player.SendWarningMessage("There are currently no players banned.");
					return;
				}

				string banString = "";
				foreach (Ban b in TShock.Bans.GetBans())
				{

					if (b.IP.Trim() == "")
					{
						continue;
					}

					if (banString.Length == 0)
					{
						banString = b.IP;
					}
					else
					{
						int length = banString.Length;
						while (length > 60)
						{
							length = length - 60;
						}
						if (length + b.Name.Length >= 60)
						{
							banString += "|, " + b.IP;
						}
						else
						{
							banString += ", " + b.IP;
						}
					}
				}

				String[] banStrings = banString.Split('|');

				if (banStrings.Length == 0)
				{
					args.Player.SendErrorMessage("There are currently no players with valid IPs banned.");
					return;
				}

				if (banStrings[0].Trim() == "")
				{
					args.Player.SendErrorMessage("There are currently no bans with valid IPs found.");
					return;
				}

				args.Player.SendInfoMessage("List of IP banned players:");
				foreach (string s in banStrings)
				{
					args.Player.SendInfoMessage(s);
				}
				return;
				#endregion List ip bans
			}

			if (args.Parameters.Count >= 2)
			{
				if (args.Parameters[0].ToLower() == "add")
				{
					#region Add ban
					string plStr = args.Parameters[1];
					var players = TShock.Utils.FindPlayer(plStr);
					if (players.Count == 0)
					{
						args.Player.SendErrorMessage("Invalid player!");
					}
					else if (players.Count > 1)
					{
						TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
					}
					else
					{
						string reason = args.Parameters.Count > 2
											? String.Join(" ", args.Parameters.GetRange(2, args.Parameters.Count - 2))
											: "Misbehavior.";
						if (!TShock.Utils.Ban(players[0], reason, !args.Player.RealPlayer, args.Player.UserAccountName))
						{
							args.Player.SendErrorMessage("You can't ban another admin!");
						}
					}
					return;
					#endregion Add ban
				}
				else if (args.Parameters[0].ToLower() == "addip")
				{
					#region Add ip ban
					string ip = args.Parameters[1];
					string reason = args.Parameters.Count > 2
										? String.Join(" ", args.Parameters.GetRange(2, args.Parameters.Count - 2))
										: "Manually added IP address ban.";
					TShock.Bans.AddBan(ip, "", "", reason, false, args.Player.UserAccountName);
					args.Player.SendSuccessMessage(ip + " banned.");
					return;
					#endregion Add ip ban
				}
				else if (args.Parameters[0].ToLower() == "delip")
				{
					#region Delete ip ban
					var ip = args.Parameters[1];
					var ban = TShock.Bans.GetBanByIp(ip);
					if (ban != null)
					{
						if (TShock.Bans.RemoveBan(ban.IP))
							args.Player.SendSuccessMessage(string.Format("Unbanned {0} ({1})!", ban.Name, ban.IP));
						else
							args.Player.SendErrorMessage(string.Format("Failed to unban {0} ({1})!", ban.Name, ban.IP));
					}
					else
					{
						args.Player.SendErrorMessage(string.Format("No bans for ip {0} exist", ip));
					}
					return;
					#endregion Delete ip ban
				}
				else if (args.Parameters[0].ToLower() == "del")
				{
					#region Delete ban
					string plStr = args.Parameters[1];
					var ban = TShock.Bans.GetBanByName(plStr, false);
					if (ban != null)
					{
						if (TShock.Bans.RemoveBan(ban.Name, true))
							args.Player.SendSuccessMessage(string.Format("Unbanned {0} ({1})!", ban.Name, ban.IP));
						else
							args.Player.SendErrorMessage(string.Format("Failed to unban {0} ({1})!", ban.Name, ban.IP));
					}
					else
					{
						args.Player.SendErrorMessage(string.Format("No bans for player {0} exist", plStr));
					}
					return;
					#endregion Delete ban
				}

				#region Clear bans
				if (args.Parameters[0].ToLower() == "clear")
				{
					if (args.Parameters.Count < 1 && ClearBansCode == -1)
					{
						ClearBansCode = new Random().Next(0, short.MaxValue);
						args.Player.SendInfoMessage("ClearBans Code: " + ClearBansCode);
						return;
					}
					if (args.Parameters.Count < 1)
					{
						args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /ban clear <code>");
						return;
					}

					int num;
					if (!int.TryParse(args.Parameters[1], out num))
					{
						args.Player.SendErrorMessage("Invalid syntax! Expected a number, didn't get one.");
						return;
					}

					if (num == ClearBansCode)
					{
						ClearBansCode = -1;
						if (TShock.Bans.ClearBans())
						{
							Log.ConsoleInfo("Bans cleared.");
							args.Player.SendSuccessMessage("Bans cleared.");
						}
						else
						{
							args.Player.SendErrorMessage("Failed to clear bans.");
						}
					}
					else
					{
						args.Player.SendErrorMessage("Incorrect clear code.");
					}
				}
				return;
				#endregion Clear bans
			}
			args.Player.SendErrorMessage("Invalid syntax or old command provided.");
			args.Player.SendErrorMessage("Type /ban help for more information.");
		}

		private static int ClearBansCode = -1;

		private static void Whitelist(CommandArgs args)
		{
			if (args.Parameters.Count == 1)
			{
				using (var tw = new StreamWriter(FileTools.WhitelistPath, true))
				{
					tw.WriteLine(args.Parameters[0]);
				}
				args.Player.SendSuccessMessage("Added " + args.Parameters[0] + " to the whitelist.");
			}
		}

		private static void DisplayLogs(CommandArgs args)
		{
			args.Player.DisplayLogs = (!args.Player.DisplayLogs);
			args.Player.SendSuccessMessage("You will " + (args.Player.DisplayLogs ? "now" : "no longer") + " receive logs.");
		}

		private static void SaveSSC(CommandArgs args)
		{
			if (TShock.Config.ServerSideCharacter)
			{
				args.Player.SendSuccessMessage("SSC has been saved.");
				foreach (TSPlayer player in TShock.Players)
				{
					if (player != null && player.IsLoggedIn && !player.IgnoreActionsForClearingTrashCan)
					{
						TShock.CharacterDB.InsertPlayerData(player);
					}
				}
			}
		}

		private static void OverrideSSC(CommandArgs args)
		{
			if (!TShock.Config.ServerSideCharacter)
			{
				args.Player.SendErrorMessage("Server Side Characters is disabled.");
				return;
			}
			if( args.Parameters.Count < 1 )
			{
				args.Player.SendErrorMessage("Correct usage: /overridessc|/ossc <player name>");
				return;
			}

			string playerNameToMatch = string.Join(" ", args.Parameters);
			var matchedPlayers = TShock.Utils.FindPlayer(playerNameToMatch);
			if( matchedPlayers.Count < 1 )
			{
				args.Player.SendErrorMessage("No players matched \"{0}\".", playerNameToMatch);
				return;
			}
			else if( matchedPlayers.Count > 1 )
			{
				TShock.Utils.SendMultipleMatchError(args.Player, matchedPlayers.Select(p => p.Name));
				return;
			}

			TSPlayer matchedPlayer = matchedPlayers[0];
			if (matchedPlayer.IsLoggedIn)
			{
				args.Player.SendErrorMessage("Player \"{0}\" is already logged in.", matchedPlayer.Name);
				return;
			}
			if (!matchedPlayer.LoginFailsBySsi)
			{
				args.Player.SendErrorMessage("Player \"{0}\" has to perform a /login attempt first.", matchedPlayer.Name);
				return;
			}
			if (matchedPlayer.IgnoreActionsForClearingTrashCan)
			{
				args.Player.SendErrorMessage("Player \"{0}\" has to reconnect first.", matchedPlayer.Name);
				return;
			}

			TShock.CharacterDB.InsertPlayerData(matchedPlayer);
			args.Player.SendSuccessMessage("SSC of player \"{0}\" has been overriden.", matchedPlayer.Name);
		}

        private static void ForceXmas(CommandArgs args)
        {
            if(args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Usage: /forcexmas [true/false]");
                args.Player.SendInfoMessage(
                    String.Format("The server is currently {0} force Christmas mode.",
                                (TShock.Config.ForceXmas ? "in" : "not in")));
                return;
            }

            if(args.Parameters[0].ToLower() == "true")
            {
                TShock.Config.ForceXmas = true;
                Main.checkXMas();
            }
            else if(args.Parameters[0].ToLower() == "false")
            {
                TShock.Config.ForceXmas = false;
                Main.checkXMas();
            }
            else
            {
                args.Player.SendErrorMessage("Usage: /forcexmas [true/false]");
                return;
            }

            args.Player.SendInfoMessage(
                    String.Format("The server is currently {0} force Christmas mode.",
                                (TShock.Config.ForceXmas ? "in" : "not in")));
        }

		private static void TempGroup(CommandArgs args)
        {
            if (args.Parameters.Count < 2)
            {
                args.Player.SendInfoMessage("Invalid usage");
                args.Player.SendInfoMessage("Usage: /tempgroup <username> <new group>");
                return;
            }

            List<TSPlayer> ply = TShock.Utils.FindPlayer(args.Parameters[0]);
            if(ply.Count < 1)
            {
                args.Player.SendErrorMessage(string.Format("Could not find player {0}.", args.Parameters[0]));
                return;
            }

            if (ply.Count > 1)
            {
				TShock.Utils.SendMultipleMatchError(args.Player, ply.Select(p => p.UserAccountName));
            }

            if(!TShock.Groups.GroupExists(args.Parameters[1]))
            {
                args.Player.SendErrorMessage(string.Format("Could not find group {0}", args.Parameters[1]));
                return;
            }

            Group g = TShock.Utils.GetGroup(args.Parameters[1]);

            ply[0].tempGroup = g;

            args.Player.SendSuccessMessage(string.Format("You have changed {0}'s group to {1}", ply[0].Name, g.Name));
            ply[0].SendSuccessMessage(string.Format("Your group has temporarily been changed to {0}", g.Name));
        }

		#endregion Player Management Commands

		#region Server Maintenence Commands

		private static void Broadcast(CommandArgs args)
		{
			string message = string.Join(" ", args.Parameters);

			TShock.Utils.Broadcast(
				"(Server Broadcast) " + message, 
				Convert.ToByte(TShock.Config.BroadcastRGB[0]), Convert.ToByte(TShock.Config.BroadcastRGB[1]), 
				Convert.ToByte(TShock.Config.BroadcastRGB[2]));
		}

		private static void Off(CommandArgs args)
		{

			if (TShock.Config.ServerSideCharacter)
			{
				foreach (TSPlayer player in TShock.Players)
				{
					if (player != null && player.IsLoggedIn && !player.IgnoreActionsForClearingTrashCan)
					{
						player.SaveServerCharacter();
					}
				}
			}

			string reason = ((args.Parameters.Count > 0) ? "Server shutting down: " + String.Join(" ", args.Parameters) : "Server shutting down!");
			TShock.Utils.StopServer(true, reason);
		}
		
		private static void Restart(CommandArgs args)
		{
			if (Main.runningMono)
			{
				Log.ConsoleInfo("Sorry, this command has not yet been implemented in Mono.");
			}
			else
			{
				string reason = ((args.Parameters.Count > 0) ? "Server shutting down: " + String.Join(" ", args.Parameters) : "Server shutting down!");
				TShock.Utils.RestartServer(true, reason);
			}
		}

		private static void OffNoSave(CommandArgs args)
		{
			string reason = ((args.Parameters.Count > 0) ? "Server shutting down: " + String.Join(" ", args.Parameters) : "Server shutting down!");
			TShock.Utils.StopServer(false, reason);
		}

		private static void CheckUpdates(CommandArgs args)
		{
            args.Player.SendInfoMessage("An update check has been queued.");
			ThreadPool.QueueUserWorkItem(UpdateManager.CheckUpdate);
		}

        private static void UpdatePlugins(CommandArgs args)
        {
            args.Player.SendInfoMessage("Starting plugin update process:");
            args.Player.SendInfoMessage("This may take a while, do not turn off the server!");
        }

		private static void ManageRest(CommandArgs args)
		{
			string subCommand = "help";
			if (args.Parameters.Count > 0)
				subCommand = args.Parameters[0];

			switch(subCommand.ToLower())
			{
				case "listusers":
				{
					int pageNumber;
					if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
						return;

					Dictionary<string,int> restUsersTokens = new Dictionary<string,int>();
					foreach (Rests.SecureRest.TokenData tokenData in TShock.RestApi.Tokens.Values)
					{
						if (restUsersTokens.ContainsKey(tokenData.Username))
							restUsersTokens[tokenData.Username]++;
						else
							restUsersTokens.Add(tokenData.Username, 1);
					}

					List<string> restUsers = new List<string>(
						restUsersTokens.Select(ut => string.Format("{0} ({1} tokens)", ut.Key, ut.Value)));

					PaginationTools.SendPage(
						args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(restUsers), new PaginationTools.Settings {
							NothingToDisplayString = "There are currently no active REST users.",
							HeaderFormat = "Active REST Users ({0}/{1}):",
							FooterFormat = "Type /rest listusers {0} for more."
						}
					);

					break;
				}
				case "destroytokens":
				{
					TShock.RestApi.Tokens.Clear();
					args.Player.SendSuccessMessage("All REST tokens have been destroyed.");
					break;
				}
				default:
				{
					args.Player.SendInfoMessage("Available REST Sub-Commands:");
					args.Player.SendMessage("listusers - Lists all REST users and their current active tokens.", Color.White);
					args.Player.SendMessage("destroytokens - Destroys all current REST tokens.", Color.White);
					break;
				}
			}
		}

		#endregion Server Maintenence Commands

        #region Cause Events and Spawn Monsters Commands

        private static void DropMeteor(CommandArgs args)
		{
			WorldGen.spawnMeteor = false;
			WorldGen.dropMeteor();
            args.Player.SendInfoMessage("A meteor has been triggered.");
		}

		private static void Fullmoon(CommandArgs args)
		{
			TSPlayer.Server.SetFullMoon(true);
			TShock.Utils.Broadcast(string.Format("{0} turned on the full moon.", args.Player.Name), Color.Green);
		}

		private static void Bloodmoon(CommandArgs args)
		{
			TSPlayer.Server.SetBloodMoon(true);
			TShock.Utils.Broadcast(string.Format("{0} turned on the blood moon.", args.Player.Name), Color.Green);
		}

		private static void Eclipse(CommandArgs args)
		{
			TSPlayer.Server.SetEclipse(true);
			TShock.Utils.Broadcast(string.Format("{0} has forced an Eclipse!", args.Player.Name), Color.Green);
		}
		
		private static void Invade(CommandArgs args)
		{
			if (Main.invasionSize <= 0)
			{
				if (args.Parameters.Count != 1)
				{
					args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /invade <invasion type>");
					return;
				}

				switch (args.Parameters[0].ToLower())
				{
					case "goblin":
					case "goblins":
					case "goblin army":
						TSPlayer.All.SendInfoMessage("{0} has started a goblin army invasion.", args.Player.Name);
						TShock.StartInvasion(1);
						break;
					case "snowman":
					case "snowmen":
					case "snow legion":
						TSPlayer.All.SendInfoMessage("{0} has started a snow legion invasion.", args.Player.Name);
						TShock.StartInvasion(2);
						break;
					case "pirate":
					case "pirates":
						TSPlayer.All.SendInfoMessage("{0} has started a pirate invasion.", args.Player.Name);
						TShock.StartInvasion(3);
						break;
				}
			}
			else
			{
                TSPlayer.All.SendInfoMessage("{0} has ended the invasion.", args.Player.Name);
				Main.invasionSize = 0;
			}
		}

		private static void PumpkinInvasion(CommandArgs args)
		{
			TSPlayer.Server.SetTime(false, 0.0);

			int wave = 1;
			if (args.Parameters.Count != 0)
				int.TryParse(args.Parameters[0], out wave);

			Main.pumpkinMoon = true;
			Main.bloodMoon = false;
			NPC.waveKills = 0f;
			NPC.waveCount = wave;
			string text = "Pumpkin Invasion started at wave;" + wave;
			if (Main.netMode == 0)
			{
				Main.NewText(text, 175, 75, 255, false);
				return;
			}
			if (Main.netMode == 2)
			{
				NetMessage.SendData(25, -1, -1, text, 255, 175f, 75f, 255f, 0);
			}
		}
        private static void Hardmode(CommandArgs args)
        {
			if (Main.hardMode)
			{
				Main.hardMode = false;
				args.Player.SendSuccessMessage("Hardmode is now off.");
			}
			else
			{
				if (!TShock.Config.DisableHardmode)
				{
					WorldGen.StartHardmode();
					args.Player.SendSuccessMessage("Hardmode is now on.");
				}
				else
				{
					args.Player.SendErrorMessage("Hardmode is disabled via config.");
				}
			}
        }

		private static void SpawnBoss(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /spawnboss <boss type> [amount]");
				return;
			}

			int amount = 1;
			if (args.Parameters.Count == 2 && (!int.TryParse(args.Parameters[1], out amount) || amount <= 0))
			{
				args.Player.SendErrorMessage("Invalid boss amount!");
				return;
			}

			NPC npc = new NPC();
			switch (args.Parameters[0].ToLower())
			{
				case "brain":
				case "brain of cthulhu":
					npc.SetDefaults(266);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned the Brain of Cthulhu {1} time(s).", args.Player.Name, amount);
					return;
				case "destroyer":
					npc.SetDefaults(134);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned the Destroyer {1} time(s).", args.Player.Name, amount);
					return;
				case "eater":
				case "eater of worlds":
					npc.SetDefaults(13);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned the Eater of Worlds {1} time(s).", args.Player.Name, amount);
					return;
				case "eye":
				case "eye of cthulhu":
					npc.SetDefaults(4);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned the Eye of Cthulhu {1} time(s).", args.Player.Name, amount);
					return;
				case "golem":
					npc.SetDefaults(245);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned Golem {1} time(s).", args.Player.Name, amount);
					return;
				case "king":
				case "king slime":
					npc.SetDefaults(50);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned King Slime {1} time(s).", args.Player.Name, amount);
					return;
				case "plantera":
					npc.SetDefaults(262);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned Plantera {1} time(s).", args.Player.Name, amount);
					return;
				case "prime":
				case "skeletron prime":
					npc.SetDefaults(127);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned Skeletron Prime {1} time(s).", args.Player.Name, amount);
					return;
				case "queen":
				case "queen bee":
					npc.SetDefaults(222);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned Queen Bee {1} time(s).", args.Player.Name, amount);
					return;
				case "skeletron":
					npc.SetDefaults(35);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned Skeletron {1} time(s).", args.Player.Name, amount);
					return;
				case "twins":
					TSPlayer.Server.SetTime(false, 0.0);
					npc.SetDefaults(125);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					npc.SetDefaults(126);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} has spawned the Twins {1} time(s).", args.Player.Name, amount);
					return;
				case "wof":
				case "wall of flesh":
					if (Main.wof >= 0)
					{
						args.Player.SendErrorMessage("There is already a Wall of Flesh!");
						return;
					}
					if (args.Player.Y / 16f < Main.maxTilesY - 205)
					{
						args.Player.SendErrorMessage("You must spawn the Wall of Flesh in hell!");
						return;
					}
					NPC.SpawnWOF(new Vector2(args.Player.X, args.Player.Y));
					TSPlayer.All.SendSuccessMessage("{0} has spawned the Wall of Flesh.", args.Player.Name);
					return;
				default:
					args.Player.SendErrorMessage("Invalid boss type!");
					return;
			}
		}

		private static void SpawnMob(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /spawnmob <mob type> [amount]");
				return;
			}
			if (args.Parameters[0].Length == 0)
			{
				args.Player.SendErrorMessage("Invalid mob type!");
				return;
			}

			int amount = 1;
			if (args.Parameters.Count == 2 && !int.TryParse(args.Parameters[1], out amount))
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /spawnmob <mob type> [amount]");
				return;
			}

			amount = Math.Min(amount, Main.maxNPCs);

			var npcs = TShock.Utils.GetNPCByIdOrName(args.Parameters[0]);
			if (npcs.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid mob type!");
			}
			else if (npcs.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, npcs.Select(n => n.name));
			}
			else
			{
				var npc = npcs[0];
				if (npc.type >= 1 && npc.type < Main.maxNPCTypes && npc.type != 113)
				{
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY, 50, 20);
					TSPlayer.All.SendSuccessMessage("{0} has spawned {1} {2} time(s).", args.Player.Name, npc.name, amount);
				}
				else if (npc.type == 113)
				{
					if (Main.wof >= 0 || (args.Player.Y / 16f < (Main.maxTilesY - 205)))
					{
						args.Player.SendErrorMessage("Can't spawn Wall of Flesh!");
						return;
					}
					NPC.SpawnWOF(new Vector2(args.Player.X, args.Player.Y));
					TSPlayer.All.SendSuccessMessage("{0} has spawned Wall of Flesh!", args.Player.Name);
				}
				else
				{
					args.Player.SendErrorMessage("Invalid mob type!");
				}
			}
		}

		#endregion Cause Events and Spawn Monsters Commands

		#region Teleport Commands

		private static void Home(CommandArgs args)
		{
			args.Player.Spawn();
			args.Player.SendSuccessMessage("Teleported to your spawnpoint.");
		}

		private static void Spawn(CommandArgs args)
		{
			if (args.Player.Teleport(Main.spawnTileX*16, (Main.spawnTileY*16) -48))
				args.Player.SendSuccessMessage("Teleported to the map's spawnpoint.");
		}

		private static void TP(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tp <player>");
				args.Player.SendErrorMessage("                               /tp <x> <y>");
				return;
			}

			if(args.Parameters.Count == 2)
			{
				float x, y;
				if (float.TryParse(args.Parameters[0], out x) && float.TryParse(args.Parameters[1], out y))
				{
					args.Player.Teleport(x, y);
					args.Player.SendSuccessMessage("Teleported!");
				}
			}
			else
			{
				string plStr = String.Join(" ", args.Parameters);
				var players = TShock.Utils.FindPlayer(plStr);
				if (players.Count == 0)
				{
					args.Player.SendErrorMessage("Invalid user name.");
					args.Player.SendErrorMessage("Proper syntax: /tp <player>");
					args.Player.SendErrorMessage("               /tp <x> <y>");
				}

				else if (players.Count > 1)
					TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
				else if (!players[0].TPAllow && !args.Player.Group.HasPermission(Permissions.tpall))
				{
					var plr = players[0];
					args.Player.SendErrorMessage(plr.Name + " has prevented users from teleporting to them.");
					plr.SendInfoMessage(args.Player.Name + " attempted to teleport to you.");
				}
				else
				{
					var plr = players[0];
					if (args.Player.Teleport(plr.TileX * 16, plr.TileY * 16 ))
					{
						args.Player.SendSuccessMessage(string.Format("Teleported to {0}.", plr.Name));
						if (!args.Player.Group.HasPermission(Permissions.tphide))
							plr.SendInfoMessage(args.Player.Name + " teleported to you.");
					}
				}
			}


	}

		private static void TPHere(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tphere <player> ");
				return;
			}

			string plStr = String.Join(" ", args.Parameters);

			if (plStr == "all" || plStr == "*")
			{
				args.Player.SendInfoMessage(string.Format("You brought all players here."));
				for (int i = 0; i < Main.maxPlayers; i++)
				{
					if (Main.player[i].active && (Main.player[i] != args.TPlayer))
					{
						if (TShock.Players[i].Teleport(args.Player.TileX*16, args.Player.TileY*16 ))
							TShock.Players[i].SendSuccessMessage(string.Format("You were teleported to {0}.", args.Player.Name) + ".");
					}
				}
				return;
			}

			var players = TShock.Utils.FindPlayer(plStr);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid player!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else
			{
				var plr = players[0];
				if (plr.Teleport(args.Player.TileX*16, args.Player.TileY*16 ))
				{
					plr.SendInfoMessage("You were teleported to {0}.", args.Player.Name);
					args.Player.SendSuccessMessage("You teleported {0} here.", plr.Name);
				}
			}
		}

		private static void TPAllow(CommandArgs args)
		{
			if (!args.Player.TPAllow)
				args.Player.SendSuccessMessage("You have removed your teleportation protection.");
			if (args.Player.TPAllow)
                args.Player.SendSuccessMessage("You have enabled teleportation protection.");
			args.Player.TPAllow = !args.Player.TPAllow;
		}

		private static void Warp(CommandArgs args)
		{
		    bool hasManageWarpPermission = args.Player.Group.HasPermission(Permissions.managewarp);
            if (args.Parameters.Count < 1)
            {
                if (hasManageWarpPermission)
                {
                    args.Player.SendInfoMessage("Invalid syntax! Proper syntax: /warp [command] [arguments]");
                    args.Player.SendInfoMessage("Commands: add, del, hide, list, send, [warpname]");
                    args.Player.SendInfoMessage("Arguments: add [warp name], del [warp name], list [page]");
                    args.Player.SendInfoMessage("Arguments: send [player] [warp name], hide [warp name] [Enable(true/false)]");
                    args.Player.SendInfoMessage("Examples: /warp add foobar, /warp hide foobar true, /warp foobar");
                    return;
                }
                else
                {
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /warp [name] or /warp list <page>");
                    return;
                }
            }

			if (args.Parameters[0].Equals("list"))
            {
                #region List warps
				int pageNumber;
				if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
					return;
				IEnumerable<string> warpNames = from warp in TShock.Warps.Warps
												where !warp.IsPrivate
												select warp.Name;
				PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(warpNames),
					new PaginationTools.Settings
					{
						HeaderFormat = "Warps ({0}/{1}):",
						FooterFormat = "Type /warp list {0} for more.",
						NothingToDisplayString = "There are currently no warps defined."
					});
                #endregion
            }
            else if (args.Parameters[0].ToLower() == "add" && hasManageWarpPermission)
            {
                #region Add warp
                if (args.Parameters.Count == 2)
                {
                    string warpName = args.Parameters[1];
                    if (warpName == "list" || warpName == "hide" || warpName == "del" || warpName == "add")
                    {
                        args.Player.SendErrorMessage("Name reserved, use a different name.");
                    }
                    else if (TShock.Warps.Add(args.Player.TileX, args.Player.TileY, warpName))
                    {
                        args.Player.SendSuccessMessage("Warp added: " + warpName);
						foreach (TSPlayer tsplr in TShock.Players)
						{
							if (tsplr != null && tsplr.IsRaptor && tsplr.Group.HasPermission(Permissions.managewarp))
								tsplr.SendRaptorWarp(TShock.Warps.Find(warpName));
						}
                    }
                    else
                    {
                        args.Player.SendErrorMessage("Warp " + warpName + " already exists.");
                    }
                }
                else
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /warp add [name]");
                #endregion
            }
            else if (args.Parameters[0].ToLower() == "del" && hasManageWarpPermission)
            {
                #region Del warp
                if (args.Parameters.Count == 2)
                {
                    string warpName = args.Parameters[1];
					if (TShock.Warps.Remove(warpName))
					{
						args.Player.SendSuccessMessage("Warp deleted: " + warpName);
						foreach (TSPlayer tsplr in TShock.Players)
						{
							if (tsplr != null && tsplr.IsRaptor && tsplr.Group.HasPermission(Permissions.managewarp))
								tsplr.SendRaptorWarpDeletion(warpName);
						}
					}
					else
						args.Player.SendErrorMessage("Could not find the specified warp.");
                }
                else
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /warp del [name]");
                #endregion
            }
            else if (args.Parameters[0].ToLower() == "hide" && hasManageWarpPermission)
            {
                #region Hide warp
                if (args.Parameters.Count == 3)
                {
                    string warpName = args.Parameters[1];
                    bool state = false;
                    if (Boolean.TryParse(args.Parameters[2], out state))
                    {
                        if (TShock.Warps.Hide(args.Parameters[1], state))
                        {
                            if (state)
                                args.Player.SendSuccessMessage("Warp " + warpName + " is now private.");
                            else
                                args.Player.SendSuccessMessage("Warp " + warpName + " is now public.");
                        }
                        else
                            args.Player.SendErrorMessage("Could not find specified warp.");
                    }
                    else
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /warp hide [name] <true/false>");
                }
                else
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /warp hide [name] <true/false>");
                #endregion
            }
            else if (args.Parameters[0].ToLower() == "send" && args.Player.Group.HasPermission(Permissions.tphere))
            {
                #region Warp send
                if (args.Parameters.Count < 3)
                {
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /warp send [player] [warpname]");
                    return;
                }

                var foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
                if (foundplr.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player!");
                    return;
                }
                else if (foundplr.Count > 1)
                {
					TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
                    return;
                }

                string warpName = args.Parameters[2];
                var warp = TShock.Warps.Find(warpName);
                var plr = foundplr[0];
				if (warp.Position != Point.Zero)
				{
					if (plr.Teleport(warp.Position.X * 16, warp.Position.Y * 16))
					{
						plr.SendSuccessMessage(String.Format("{0} warped you to {1}.", args.Player.Name, warpName));
						args.Player.SendSuccessMessage(String.Format("You warped {0} to {1}.", plr.Name, warpName));
					}
				}
				else
				{
					args.Player.SendErrorMessage("Specified warp not found.");
				}
                #endregion
            }
            else
            {
                string warpName = String.Join(" ", args.Parameters);
                var warp = TShock.Warps.Find(warpName);
                if (warp != null)
                {
					if (args.Player.Teleport(warp.Position.X * 16, warp.Position.Y * 16))
                        args.Player.SendSuccessMessage("Warped to " + warpName + ".");
                }
                else
                {
                    args.Player.SendErrorMessage("The specified warp was not found.");
                }
            }
		}

		#endregion Teleport Commands

		#region Group Management

		private static void Group(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendInfoMessage("Invalid syntax! Proper syntax: /group <command> [arguments]");
				args.Player.SendInfoMessage("Commands: add, addperm, del, delperm, list, listperm");
				args.Player.SendInfoMessage("Arguments: add <group name>, addperm <group name> <permissions...>, del <group name>");
				args.Player.SendInfoMessage("Arguments: delperm <group name> <permissions...>, list [page], listperm <group name> [page]");
				return;
			}

			switch (args.Parameters[0].ToLower())
			{
				case "add":
					#region Add group
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /group add <group name> [permissions]");
							return;
						}

						string groupName = args.Parameters[1];
						args.Parameters.RemoveRange(0, 2);
						string permissions = String.Join(",", args.Parameters);

						try
						{
							string response = TShock.Groups.AddGroup(groupName, permissions);
							if (response.Length > 0)
							{
								args.Player.SendSuccessMessage(response);
							}
						}
						catch (GroupManagerException ex)
						{
							args.Player.SendErrorMessage(ex.ToString());
						}
					}
					#endregion
					return;
				case "addperm":
					#region Add permissions
					{
						if (args.Parameters.Count < 3)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /group addperm <group name> <permissions...>");
							return;
						}

						string groupName = args.Parameters[1];
						args.Parameters.RemoveRange(0, 2);
						if (groupName == "*")
						{
							foreach (Group g in TShock.Groups)
							{
								TShock.Groups.AddPermissions(g.Name, args.Parameters);
							}
							args.Player.SendSuccessMessage("Modified all groups.");
							return;
						}
						try
						{
							string response = TShock.Groups.AddPermissions(groupName, args.Parameters);
							if (response.Length > 0)
							{
								args.Player.SendSuccessMessage(response);
							}
							return;
						}
						catch (GroupManagerException ex)
						{
							args.Player.SendErrorMessage(ex.ToString());
						}
					}
					#endregion
					return;

				case "parent":
					#region Parent
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /group parent <group name> [new parent group name]");
							return;
						}

						string groupName = args.Parameters[1];
						Group group = TShock.Groups.GetGroupByName(groupName);
						if (group == null)
						{
							args.Player.SendErrorMessage("No such group \"{0}\".", groupName);
							return;
						}

						if (args.Parameters.Count > 2)
						{
							string newParentGroupName = string.Join(" ", args.Parameters.Skip(2));
							if (!string.IsNullOrWhiteSpace(newParentGroupName) && !TShock.Groups.GroupExists(newParentGroupName))
							{
								args.Player.SendErrorMessage("No such group \"{0}\".", newParentGroupName);
								return;
							}

							try
							{
								TShock.Groups.UpdateGroup(groupName, newParentGroupName, group.Permissions, group.ChatColor, group.Suffix, group.Prefix);

								if (!string.IsNullOrWhiteSpace(newParentGroupName))
									args.Player.SendSuccessMessage("Parent of group \"{0}\" set to \"{1}\".", groupName, newParentGroupName);
								else
									args.Player.SendSuccessMessage("Removed parent of group \"{0}\".", groupName);
							}
							catch (GroupManagerException ex)
							{
								args.Player.SendErrorMessage(ex.Message);
							}
						}
						else
						{
							if (group.Parent != null)
								args.Player.SendSuccessMessage("Parent of \"{0}\" is \"{1}\".", group.Name, group.Parent.Name);
							else
								args.Player.SendSuccessMessage("Group \"{0}\" has no parent.", group.Name);
						}
					}
					#endregion
					return;
				case "suffix":
					#region Suffix
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /group suffix <group name> [new suffix]");
							return;
						}

						string groupName = args.Parameters[1];
						Group group = TShock.Groups.GetGroupByName(groupName);
						if (group == null)
						{
							args.Player.SendErrorMessage("No such group \"{0}\".", groupName);
							return;
						}

						if (args.Parameters.Count > 2)
						{
							string newSuffix = string.Join(" ", args.Parameters.Skip(2));

							try
							{
								TShock.Groups.UpdateGroup(groupName, group.ParentName, group.Permissions, group.ChatColor, newSuffix, group.Prefix);

								if (!string.IsNullOrWhiteSpace(newSuffix))
									args.Player.SendSuccessMessage("Suffix of group \"{0}\" set to \"{1}\".", groupName, newSuffix);
								else
									args.Player.SendSuccessMessage("Removed suffix of group \"{0}\".", groupName);
							}
							catch (GroupManagerException ex)
							{
								args.Player.SendErrorMessage(ex.Message);
							}
						}
						else
						{
							if (!string.IsNullOrWhiteSpace(group.Suffix))
								args.Player.SendSuccessMessage("Suffix of \"{0}\" is \"{1}\".", group.Name, group.Suffix);
							else
								args.Player.SendSuccessMessage("Group \"{0}\" has no suffix.", group.Name);
						}
					}
					#endregion
					return;
				case "prefix":
					#region Prefix
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /group prefix <group name> [new prefix]");
							return;
						}

						string groupName = args.Parameters[1];
						Group group = TShock.Groups.GetGroupByName(groupName);
						if (group == null)
						{
							args.Player.SendErrorMessage("No such group \"{0}\".", groupName);
							return;
						}

						if (args.Parameters.Count > 2)
						{
							string newPrefix = string.Join(" ", args.Parameters.Skip(2));

							try
							{
								TShock.Groups.UpdateGroup(groupName, group.ParentName, group.Permissions, group.ChatColor, group.Suffix, newPrefix);

								if (!string.IsNullOrWhiteSpace(newPrefix))
									args.Player.SendSuccessMessage("Prefix of group \"{0}\" set to \"{1}\".", groupName, newPrefix);
								else
									args.Player.SendSuccessMessage("Removed prefix of group \"{0}\".", groupName);
							}
							catch (GroupManagerException ex)
							{
								args.Player.SendErrorMessage(ex.Message);
							}
						}
						else
						{
							if (!string.IsNullOrWhiteSpace(group.Prefix))
								args.Player.SendSuccessMessage("Prefix of \"{0}\" is \"{1}\".", group.Name, group.Prefix);
							else
								args.Player.SendSuccessMessage("Group \"{0}\" has no prefix.", group.Name);
						}
					}
					#endregion
					return;
				case "color":
					#region Color
					{
						if (args.Parameters.Count < 2 || args.Parameters.Count > 3)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /group color <group name> [new color(000,000,000)]");
							return;
						}

						string groupName = args.Parameters[1];
						Group group = TShock.Groups.GetGroupByName(groupName);
						if (group == null)
						{
							args.Player.SendErrorMessage("No such group \"{0}\".", groupName);
							return;
						}

						if (args.Parameters.Count == 3)
						{
							string newColor = args.Parameters[2];

							String[] parts = newColor.Split(',');
							byte r;
							byte g;
							byte b;
							if (parts.Length == 3 && byte.TryParse(parts[0], out r) && byte.TryParse(parts[1], out g) && byte.TryParse(parts[2], out b))
							{
								try
								{
									TShock.Groups.UpdateGroup(groupName, group.ParentName, group.Permissions, newColor, group.Suffix, group.Prefix);

									args.Player.SendSuccessMessage("Color of group \"{0}\" set to \"{1}\".", groupName, newColor);
								}
								catch (GroupManagerException ex)
								{
									args.Player.SendErrorMessage(ex.Message);
								}
							}
							else
							{
								args.Player.SendErrorMessage("Invalid syntax for color, expected \"rrr,ggg,bbb\"");
							}
						}
						else
						{
							args.Player.SendSuccessMessage("Color of \"{0}\" is \"{1}\".", group.Name, group.ChatColor);
						}
					}
					#endregion
					return;
				case "del":
					#region Delete group
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /group del <group name>");
							return;
						}

						try
						{
							string response = TShock.Groups.DeleteGroup(args.Parameters[1]);
							if (response.Length > 0)
							{
								args.Player.SendSuccessMessage(response);
							}
						}
						catch (GroupManagerException ex)
						{
							args.Player.SendErrorMessage(ex.ToString());
						}
					}
					#endregion
					return;
				case "delperm":
					#region Delete permissions
					{
						if (args.Parameters.Count < 3)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /group delperm <group name> <permissions...>");
							return;
						}

						string groupName = args.Parameters[1];
						args.Parameters.RemoveRange(0, 2);
						if (groupName == "*")
						{
							foreach (Group g in TShock.Groups)
							{
								TShock.Groups.DeletePermissions(g.Name, args.Parameters);
							}
							args.Player.SendSuccessMessage("Modified all groups.");
							return;
						}
						try
						{
							string response = TShock.Groups.DeletePermissions(groupName, args.Parameters);
							if (response.Length > 0)
							{
								args.Player.SendSuccessMessage(response);
							}
							return;
						}
						catch (GroupManagerException ex)
						{
							args.Player.SendErrorMessage(ex.ToString());
						}
					}
					#endregion
					return;
				case "list":
					#region List groups
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;
						IEnumerable<string> groupNames = from grp in TShock.Groups.groups
														 select grp.Name;
						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(groupNames),
							new PaginationTools.Settings
							{
								HeaderFormat = "Groups ({0}/{1}):",
								FooterFormat = "Type /group list {0} for more."
							});
					}
					#endregion
					return;
				case "listperm":
					#region List permissions
					{
						if (args.Parameters.Count == 1)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /group listperm <group name> [page]");
							return;
						}
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
							return;

						if (!TShock.Groups.GroupExists(args.Parameters[1]))
						{
							args.Player.SendErrorMessage("Invalid group.");
							return;
						}
						Group grp = TShock.Utils.GetGroup(args.Parameters[1]);
						List<string> permissions = grp.TotalPermissions;

						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(permissions),
							new PaginationTools.Settings
							{
								HeaderFormat = "Permissions for " + grp.Name + " ({0}/{1}):",
								FooterFormat = "Type /group listperm " + grp.Name + " {0} for more.",
								NothingToDisplayString = "There are currently no permissions for " + grp.Name + "."
							});
					}
					#endregion
					return;
				case "help":
					args.Player.SendInfoMessage("Syntax: /group <command> [arguments]");
					args.Player.SendInfoMessage("Commands: add, addperm, parent, del, delperm, list, listperm");
					args.Player.SendInfoMessage("Arguments: add <group name>, addperm <group name> <permissions...>, del <group name>");
					args.Player.SendInfoMessage("Arguments: delperm <group name> <permissions...>, list [page], listperm <group name> [page]");
					return;
			}
		}
		#endregion Group Management

		#region Item Management

		private static void ItemBan(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendInfoMessage("Invalid syntax! Proper syntax: /itemban <command> [arguments]");
				args.Player.SendInfoMessage("Commands: add, allow, del, disallow, list");
				args.Player.SendInfoMessage("Arguments: add <item name>, allow <item name> <group name>");
				args.Player.SendInfoMessage("Arguments: del <item name>, disallow <item name> <group name>, list [page]");
				return;
			}

			switch (args.Parameters[0].ToLower())
			{
				case "add":
					#region Add item
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /itemban add <item name>");
							return;
						}

						List<Item> items = TShock.Utils.GetItemByIdOrName(args.Parameters[1]);
						if (items.Count == 0)
						{
							args.Player.SendErrorMessage("Invalid item.");
						}
						else if (items.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
						}
						else
						{
							TShock.Itembans.AddNewBan(items[0].name);
							args.Player.SendSuccessMessage("Banned " + items[0].name + ".");
						}
					}
					#endregion
					return;
				case "allow":
					#region Allow group to item
					{
						if (args.Parameters.Count != 3)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /itemban allow <item name> <group name>");
							return;
						}

						List<Item> items = TShock.Utils.GetItemByIdOrName(args.Parameters[1]);
						if (items.Count == 0)
						{
							args.Player.SendErrorMessage("Invalid item.");
						}
						else if (items.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
						}
						else
						{
							if (!TShock.Groups.GroupExists(args.Parameters[2]))
							{
								args.Player.SendErrorMessage("Invalid group.");
								return;
							}

							ItemBan ban = TShock.Itembans.GetItemBanByName(items[0].name);
							if (ban == null)
							{
								args.Player.SendErrorMessage(items[0].name + " is not banned.");
								return;
							}
							if (!ban.AllowedGroups.Contains(args.Parameters[2]))
							{
								TShock.Itembans.AllowGroup(items[0].name, args.Parameters[2]);
								args.Player.SendSuccessMessage(String.Format("{0} has been allowed to use {1}.", args.Parameters[2], items[0].name));
							}
							else
							{
								args.Player.SendWarningMessage(String.Format("{0} is already allowed to use {1}.", args.Parameters[2], items[0].name));
							}
						}
					}
					#endregion
					return;
				case "del":
					#region Delete item
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /itemban del <item name>");
							return;
						}

						List<Item> items = TShock.Utils.GetItemByIdOrName(args.Parameters[1]);
						if (items.Count == 0)
						{
							args.Player.SendErrorMessage("Invalid item.");
						}
						else if (items.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
						}
						else
						{
							TShock.Itembans.RemoveBan(items[0].name);
							args.Player.SendSuccessMessage("Unbanned " + items[0].name + ".");
						}
					}
					#endregion
					return;
				case "disallow":
					#region Allow group to item
					{
						if (args.Parameters.Count != 3)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /itemban disallow <item name> <group name>");
							return;
						}

						List<Item> items = TShock.Utils.GetItemByIdOrName(args.Parameters[1]);
						if (items.Count == 0)
						{
							args.Player.SendErrorMessage("Invalid item.");
						}
						else if (items.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
						}
						else
						{
							if (!TShock.Groups.GroupExists(args.Parameters[2]))
							{
								args.Player.SendErrorMessage("Invalid group.");
								return;
							}

							ItemBan ban = TShock.Itembans.GetItemBanByName(items[0].name);
							if (ban == null)
							{
								args.Player.SendErrorMessage(items[0].name + " is not banned.");
								return;
							}
							if (ban.AllowedGroups.Contains(args.Parameters[2]))
							{
								TShock.Itembans.RemoveGroup(items[0].name, args.Parameters[2]);
								args.Player.SendSuccessMessage(String.Format("{0} has been disallowed to use {1}.", args.Parameters[2], items[0].name));
							}
							else
							{
								args.Player.SendWarningMessage(String.Format("{0} is already disallowed to use {1}.", args.Parameters[2], items[0].name));
							}
						}
					}
					#endregion
					return;
				case "help":
					args.Player.SendInfoMessage("Syntax: /itemban <command> [arguments]");
					args.Player.SendInfoMessage("Commands: add, allow, del, disallow, list");
					args.Player.SendInfoMessage("Arguments: add <item name>, allow <item name> <group name>");
					args.Player.SendInfoMessage("Arguments: del <item name>, disallow <item name> <group name>, list [page]");
					return;
				case "list":
					#region List items
					int pageNumber;
					if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
						return;
					IEnumerable<string> itemNames = from itemBan in TShock.Itembans.ItemBans
													select itemBan.Name;
					PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(itemNames),
						new PaginationTools.Settings
						{
							HeaderFormat = "Item bans ({0}/{1}):",
							FooterFormat = "Type /itemban list {0} for more.",
							NothingToDisplayString = "There are currently no banned items."
						});
					#endregion
					return;
			}
		}
		#endregion Item Management

		#region Server Config Commands

		private static void SetSpawn(CommandArgs args)
		{
			Main.spawnTileX = args.Player.TileX + 1;
			Main.spawnTileY = args.Player.TileY + 3;
			SaveManager.Instance.SaveWorld(false);
			args.Player.SendSuccessMessage("Spawn has now been set at your location.");
		}

		private static void Reload(CommandArgs args)
		{
			TShock.Utils.Reload(args.Player);

			args.Player.SendSuccessMessage(
				"Configuration, permissions, and regions reload complete. Some changes may require a server restart.");
		}

		private static void ServerPassword(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /password \"<new password>\"");
				return;
			}
			string passwd = args.Parameters[0];
			TShock.Config.ServerPassword = passwd;
			args.Player.SendSuccessMessage(string.Format("Server password has been changed to: {0}.", passwd));
		}

		private static void Save(CommandArgs args)
		{
			SaveManager.Instance.SaveWorld(false);
			foreach (TSPlayer tsply in TShock.Players.Where(tsply => tsply != null))
			{
				tsply.SaveServerCharacter();
			}
			args.Player.SendSuccessMessage("Save succeeded.");
		}

		private static void Settle(CommandArgs args)
		{
			if (Liquid.panicMode)
			{
				args.Player.SendWarningMessage("Liquids are already settling!");
				return;
			}
			Liquid.StartPanic();
			args.Player.SendInfoMessage("Settling liquids.");
		}

		private static void MaxSpawns(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /maxspawns <maxspawns>");
				args.Player.SendErrorMessage("Proper syntax: /maxspawns show");
				args.Player.SendErrorMessage("Proper syntax: /maxspawns default");
				return;
			}

			if (args.Parameters[0] == "show")
			{
				args.Player.SendInfoMessage("Current maximum spawns is " + TShock.Config.DefaultMaximumSpawns + ".");
				return;
			}
			
			if(args.Parameters[0]=="default"){
				TShock.Config.DefaultMaximumSpawns = 5;
				NPC.defaultMaxSpawns = 5;
				TSPlayer.All.SendInfoMessage(string.Format("{0} changed the maximum spawns to 5.", args.Player.Name));
				return;
			}

			int amount = Convert.ToInt32(args.Parameters[0]);
			int.TryParse(args.Parameters[0], out amount);
			NPC.defaultMaxSpawns = amount;
			TShock.Config.DefaultMaximumSpawns = amount;
			TSPlayer.All.SendInfoMessage(string.Format("{0} changed the maximum spawns to {1}.", args.Player.Name, amount));
		}

		private static void SpawnRate(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /spawnrate <spawnrate>");
				args.Player.SendErrorMessage("/spawnrate show");
				args.Player.SendErrorMessage("/spawnrate default");
				return;
			}

			if (args.Parameters[0] == "show")
			{
				args.Player.SendInfoMessage("Current spawn rate is " + TShock.Config.DefaultSpawnRate + ".");
				return;
			}

			if (args.Parameters[0] == "default")
			{
				TShock.Config.DefaultSpawnRate = 600;
				NPC.defaultSpawnRate = 600;
				TSPlayer.All.SendInfoMessage(string.Format("{0} changed the spawn rate to 600.", args.Player.Name));
				return;
			}

			int amount = -1;
			if (!int.TryParse(args.Parameters[0], out amount))
			{
				args.Player.SendWarningMessage(string.Format("Invalid spawnrate ({0})", args.Parameters[0]));
				return;
			}

			if (amount < 0)
			{
				args.Player.SendWarningMessage("Spawnrate cannot be negative!");
				return;
			}

			NPC.defaultSpawnRate = amount;
			TShock.Config.DefaultSpawnRate = amount;
			TSPlayer.All.SendInfoMessage(string.Format("{0} changed the spawn rate to {1}.", args.Player.Name, amount));
		}

		#endregion Server Config Commands

		#region Time/PvpFun Commands

		private static void Time(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /time <day/night/dusk/noon/midnight>");
				return;
			}

			switch (args.Parameters[0].ToLower())
			{
				case "day":
					TSPlayer.Server.SetTime(true, 150.0);
					TSPlayer.All.SendInfoMessage("{0} nastavil denni dobu na den.", args.Player.Name);
					break;
				case "night":
					TSPlayer.Server.SetTime(false, 0.0);
                    TSPlayer.All.SendInfoMessage("{0} nastavil denni dobu na noc.", args.Player.Name);
					break;
				case "dusk":
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.All.SendInfoMessage("{0} nastavil denni dobu na vecer.", args.Player.Name);
					break;
				case "noon":
					TSPlayer.Server.SetTime(true, 27000.0);
                    TSPlayer.All.SendInfoMessage("{0} nastavil denni dobu na poledne.", args.Player.Name);
					break;
				case "midnight":
					TSPlayer.Server.SetTime(false, 16200.0);
                    TSPlayer.All.SendInfoMessage("{0} nastavil denni dobu na pulno.", args.Player.Name);
					break;
				default:
					args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /time <day/night/dusk/noon/midnight>");
					break;
			}
		}

		private static void Rain(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /rain <stop/start>");
				return;
			}

			switch (args.Parameters[0].ToLower())
			{
				case "start":
					Main.StartRain();
					TSPlayer.All.SendInfoMessage("{0} caused it to rain.", args.Player.Name);
					break;
				case "stop":
					Main.StopRain();
					TSPlayer.All.SendInfoMessage("{0} ended the downpour.", args.Player.Name);
					break;
				default:
					args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /rain <stop/start>");
					break;

			}
		}

		private static void Slap(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /slap <player> [damage]");
				return;
			}
			if (args.Parameters[0].Length == 0)
			{
				args.Player.SendErrorMessage("Invalid player!");
				return;
			}

			string plStr = args.Parameters[0];
			var players = TShock.Utils.FindPlayer(plStr);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid player!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else
			{
				var plr = players[0];
				int damage = 5;
				if (args.Parameters.Count == 2)
				{
					int.TryParse(args.Parameters[1], out damage);
				}
				if (!args.Player.Group.HasPermission(Permissions.kill))
				{
					damage = TShock.Utils.Clamp(damage, 15, 0);
				}
				plr.DamagePlayer(damage);
				TSPlayer.All.SendInfoMessage("{0} slapped {1} for {2} damage.", args.Player.Name, plr.Name, damage);
				Log.Info("{0} slapped {1} for {2} damage.", args.Player.Name, plr.Name, damage);
			}
		}

		#endregion Time/PvpFun Commands

        #region Region Commands

		private static void Region(CommandArgs args)
		{
			string cmd = "help";
			if (args.Parameters.Count > 0)
			{
				cmd = args.Parameters[0].ToLower();
			}
			switch (cmd)
			{
				case "name":
					{
						{
							args.Player.SendMessage("Pro zobrazeni jmena regionu uhod do nejakeho bloku, ktery do neho patri.", Color.Yellow);
							args.Player.AwaitingName = true;
							args.Player.AwaitingNameParameters = args.Parameters.Skip(1).ToArray();
						}
						break;
					}
				case "set":
					{
						int choice = 0;
						if (args.Parameters.Count == 2 &&
							int.TryParse(args.Parameters[1], out choice) &&
							choice >= 1 && choice <= 2)
						{
							args.Player.SendMessage("Uhod do bloku ktery bude bod " + choice, Color.Yellow);
							args.Player.AwaitingTempPoint = choice;
						}
						else
						{
							args.Player.SendMessage("Chybne zadani prikazu! Sprave zadani: /region set <1/2>", Color.Red);
						}
						break;
					}
				case "define":
					{
						if (args.Parameters.Count > 1)
						{
							if (!args.Player.TempPoints.Any(p => p == Point.Zero))
							{
								string regionName = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
								var x = Math.Min(args.Player.TempPoints[0].X, args.Player.TempPoints[1].X);
								var y = Math.Min(args.Player.TempPoints[0].Y, args.Player.TempPoints[1].Y);
								var width = Math.Abs(args.Player.TempPoints[0].X - args.Player.TempPoints[1].X);
								var height = Math.Abs(args.Player.TempPoints[0].Y - args.Player.TempPoints[1].Y);

								if (TShock.Regions.AddRegion(x, y, width, height, regionName, args.Player.UserAccountName,
															 Main.worldID.ToString()))
								{
									args.Player.TempPoints[0] = Point.Zero;
									args.Player.TempPoints[1] = Point.Zero;
<<<<<<< HEAD
									args.Player.SendMessage("Vytvoren region " + regionName, Color.Yellow);
=======
									args.Player.SendMessage("Set region " + regionName, Color.Yellow);

									foreach (TSPlayer tsplr in TShock.Players)
									{
										if (tsplr != null && tsplr.IsRaptor && tsplr.Group.HasPermission(Permissions.manageregion))
											tsplr.SendRaptorRegion(TShock.Regions.GetRegionByName(regionName));
									}
>>>>>>> refs/remotes/NyxStudios/general-devel
								}
								else
								{
									args.Player.SendMessage("Region " + regionName + " jiz existuje", Color.Red);
								}
							}
							else
							{
								args.Player.SendMessage("Nejsou zvoleny body oznacujici region", Color.Red);
							}
						}
						else
                            args.Player.SendMessage("Chybne zadani prikazu! Sprave zadani: /region define <jmeno regionu>", Color.Red);
						break;
					}
				case "protect":
					{
						if (args.Parameters.Count == 3)
						{
							string regionName = args.Parameters[1];
							if (args.Parameters[2].ToLower() == "true")
							{
								if (TShock.Regions.SetRegionState(regionName, true))
									args.Player.SendMessage("Protected region " + regionName, Color.Yellow);
								else
									args.Player.SendMessage("Could not find specified region", Color.Red);
							}
							else if (args.Parameters[2].ToLower() == "false")
							{
								if (TShock.Regions.SetRegionState(regionName, false))
									args.Player.SendMessage("Unprotected region " + regionName, Color.Yellow);
								else
									args.Player.SendMessage("Could not find specified region", Color.Red);
							}
							else
								args.Player.SendMessage("Invalid syntax! Proper syntax: /region protect <name> <true/false>", Color.Red);
						}
						else
							args.Player.SendMessage("Invalid syntax! Proper syntax: /region protect <name> <true/false>", Color.Red);
						break;
					}
				case "delete":
					{
						if (args.Parameters.Count > 1)
						{
							string regionName = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
							if (TShock.Regions.DeleteRegion(regionName))
<<<<<<< HEAD
								args.Player.SendMessage("Smazan region " + regionName, Color.Yellow);
=======
							{
								args.Player.SendInfoMessage("Deleted region \"{0}\".", regionName);
								foreach (TSPlayer tsplr in TShock.Players)
								{
									if (tsplr != null && tsplr.IsRaptor && tsplr.Group.HasPermission(Permissions.manageregion))
										tsplr.SendRaptorRegionDeletion(regionName);
								}
							}
>>>>>>> refs/remotes/NyxStudios/general-devel
							else
<<<<<<< HEAD
								args.Player.SendMessage("Nemohu najit zadany region", Color.Red);
=======
								args.Player.SendErrorMessage("Could not find the specified region!");
>>>>>>> refs/remotes/NyxStudios/general-devel
						}
						else
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /region delete <name>");
						break;
					}
				case "clear":
					{
						args.Player.TempPoints[0] = Point.Zero;
						args.Player.TempPoints[1] = Point.Zero;
						args.Player.SendInfoMessage("Cleared temporary points.");
						args.Player.AwaitingTempPoint = 0;
						break;
					}
				case "allow":
					{
						if (args.Parameters.Count > 2)
						{
							string playerName = args.Parameters[1];
							string regionName = "";

							for (int i = 2; i < args.Parameters.Count; i++)
							{
								if (regionName == "")
								{
									regionName = args.Parameters[2];
								}
								else
								{
									regionName = regionName + " " + args.Parameters[i];
								}
							}
							if (TShock.Users.GetUserByName(playerName) != null)
							{
								if (TShock.Regions.AddNewUser(regionName, playerName))
								{
									args.Player.SendMessage("Added user " + playerName + " to " + regionName, Color.Yellow);
								}
								else
									args.Player.SendMessage("Region " + regionName + " not found", Color.Red);
							}
							else
							{
								args.Player.SendMessage("Player " + playerName + " not found", Color.Red);
							}
						}
						else
							args.Player.SendMessage("Invalid syntax! Proper syntax: /region allow <name> <region>", Color.Red);
						break;
					}
				case "remove":
					if (args.Parameters.Count > 2)
					{
						string playerName = args.Parameters[1];
						string regionName = "";

						for (int i = 2; i < args.Parameters.Count; i++)
						{
							if (regionName == "")
							{
								regionName = args.Parameters[2];
							}
							else
							{
								regionName = regionName + " " + args.Parameters[i];
							}
						}
						if (TShock.Users.GetUserByName(playerName) != null)
						{
							if (TShock.Regions.RemoveUser(regionName, playerName))
							{
								args.Player.SendMessage("Removed user " + playerName + " from " + regionName, Color.Yellow);
							}
							else
								args.Player.SendMessage("Region " + regionName + " not found", Color.Red);
						}
						else
						{
							args.Player.SendMessage("Player " + playerName + " not found", Color.Red);
						}
					}
					else
						args.Player.SendMessage("Invalid syntax! Proper syntax: /region remove <name> <region>", Color.Red);
					break;
				case "allowg":
					{
						if (args.Parameters.Count > 2)
						{
							string group = args.Parameters[1];
							string regionName = "";

							for (int i = 2; i < args.Parameters.Count; i++)
							{
								if (regionName == "")
								{
									regionName = args.Parameters[2];
								}
								else
								{
									regionName = regionName + " " + args.Parameters[i];
								}
							}
							if (TShock.Groups.GroupExists(group))
							{
								if (TShock.Regions.AllowGroup(regionName, group))
								{
									args.Player.SendMessage("Added group " + group + " to " + regionName, Color.Yellow);
								}
								else
									args.Player.SendMessage("Region " + regionName + " not found", Color.Red);
							}
							else
							{
								args.Player.SendMessage("Group " + group + " not found", Color.Red);
							}
						}
						else
							args.Player.SendMessage("Invalid syntax! Proper syntax: /region allowg <group> <region>", Color.Red);
						break;
					}
				case "removeg":
					if (args.Parameters.Count > 2)
					{
						string group = args.Parameters[1];
						string regionName = "";

						for (int i = 2; i < args.Parameters.Count; i++)
						{
							if (regionName == "")
							{
								regionName = args.Parameters[2];
							}
							else
							{
								regionName = regionName + " " + args.Parameters[i];
							}
						}
						if (TShock.Groups.GroupExists(group))
						{
							if (TShock.Regions.RemoveGroup(regionName, group))
							{
								args.Player.SendMessage("Removed group " + group + " from " + regionName, Color.Yellow);
							}
							else
								args.Player.SendMessage("Region " + regionName + " not found", Color.Red);
						}
						else
						{
							args.Player.SendMessage("Group " + group + " not found", Color.Red);
						}
					}
					else
						args.Player.SendMessage("Invalid syntax! Proper syntax: /region removeg <group> <region>", Color.Red);
					break;
				case "list":
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;

						IEnumerable<string> regionNames = from region in TShock.Regions.Regions
														  where region.WorldID == Main.worldID.ToString()
														  select region.Name;
						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(regionNames),
							new PaginationTools.Settings
							{
								HeaderFormat = "Regions ({0}/{1}):",
								FooterFormat = "Type /region list {0} for more.",
								NothingToDisplayString = "There are currently no regions defined."
							});
						break;
					}
				case "info":
					{
						if (args.Parameters.Count == 1 || args.Parameters.Count > 4)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /region info <region> [-d] [page]");
							break;
						}

						string regionName = args.Parameters[1];
						bool displayBoundaries = args.Parameters.Skip(2).Any(
							p => p.Equals("-d", StringComparison.InvariantCultureIgnoreCase)
						);

						Region region = TShock.Regions.GetRegionByName(regionName);
						if (region == null)
						{
							args.Player.SendErrorMessage("Region \"{0}\" does not exist.", regionName);
							break;
						}

						int pageNumberIndex = displayBoundaries ? 3 : 2;
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, pageNumberIndex, args.Player, out pageNumber))
							break;

						List<string> lines = new List<string>
                        {
                            string.Format("X: {0}; Y: {1}; W: {2}; H: {3}, Z: {4}", region.Area.X, region.Area.Y, region.Area.Width, region.Area.Height, region.Z),
                            string.Concat("Owner: ", region.Owner),
                            string.Concat("Protected: ", region.DisableBuild.ToString()),
                        };

						if (region.AllowedIDs.Count > 0)
						{
							IEnumerable<string> sharedUsersSelector = region.AllowedIDs.Select(userId =>
							{
								User user = TShock.Users.GetUserByID(userId);
								if (user != null)
									return user.Name;
								else
									return string.Concat("{ID: ", userId, "}");
							});
							List<string> extraLines = PaginationTools.BuildLinesFromTerms(sharedUsersSelector.Distinct());
							extraLines[0] = "Shared with: " + extraLines[0];
							lines.AddRange(extraLines);
						}
						else
						{
							lines.Add("Region is not shared with any users.");
						}

						if (region.AllowedGroups.Count > 0)
						{
							List<string> extraLines = PaginationTools.BuildLinesFromTerms(region.AllowedGroups.Distinct());
							extraLines[0] = "Shared with groups: " + extraLines[0];
							lines.AddRange(extraLines);
						}
						else
						{
							lines.Add("Region is not shared with any groups.");
						}

						PaginationTools.SendPage(
							args.Player, pageNumber, lines, new PaginationTools.Settings
							{
								HeaderFormat = string.Format("Information About Region \"{0}\" ({{0}}/{{1}}):", region.Name),
								FooterFormat = string.Format("Type /region info {0} {{0}} for more information.", regionName)
							}
						);

						if (displayBoundaries)
						{
							Rectangle regionArea = region.Area;
							foreach (Point boundaryPoint in Utils.Instance.EnumerateRegionBoundaries(regionArea))
							{
								// Preferring dotted lines as those should easily be distinguishable from actual wires.
								if ((boundaryPoint.X + boundaryPoint.Y & 1) == 0)
								{
									// Could be improved by sending raw tile data to the client instead but not really 
									// worth the effort as chances are very low that overwriting the wire for a few 
									// nanoseconds will cause much trouble.
									Tile tile = Main.tile[boundaryPoint.X, boundaryPoint.Y];
									bool oldWireState = tile.wire();
									tile.wire(true);

									try
									{
										args.Player.SendTileSquare(boundaryPoint.X, boundaryPoint.Y, 1);
									}
									finally
									{
										tile.wire(oldWireState);
									}
								}
							}

							Timer boundaryHideTimer = null;
							boundaryHideTimer = new Timer((state) =>
							{
								foreach (Point boundaryPoint in Utils.Instance.EnumerateRegionBoundaries(regionArea))
									if ((boundaryPoint.X + boundaryPoint.Y & 1) == 0)
										args.Player.SendTileSquare(boundaryPoint.X, boundaryPoint.Y, 1);

								// ReSharper disable AccessToModifiedClosure
								Debug.Assert(boundaryHideTimer != null);
								boundaryHideTimer.Dispose();
								// ReSharper restore AccessToModifiedClosure
							},
								null, 5000, Timeout.Infinite
							);
						}

						break;
					}
				case "z":
					{
						if (args.Parameters.Count == 3)
						{
							string regionName = args.Parameters[1];
							int z = 0;
							if (int.TryParse(args.Parameters[2], out z))
							{
								if (TShock.Regions.SetZ(regionName, z))
									args.Player.SendMessage("Region's z is now " + z, Color.Yellow);
								else
									args.Player.SendMessage("Could not find specified region", Color.Red);
							}
							else
								args.Player.SendMessage("Invalid syntax! Proper syntax: /region z <name> <#>", Color.Red);
						}
						else
							args.Player.SendMessage("Invalid syntax! Proper syntax: /region z <name> <#>", Color.Red);
						break;
					}
				case "resize":
				case "expand":
					{
						if (args.Parameters.Count == 4)
						{
							int direction;
							switch (args.Parameters[2])
							{
								case "u":
								case "up":
									{
										direction = 0;
										break;
									}
								case "r":
								case "right":
									{
										direction = 1;
										break;
									}
								case "d":
								case "down":
									{
										direction = 2;
										break;
									}
								case "l":
								case "left":
									{
										direction = 3;
										break;
									}
								default:
									{
										direction = -1;
										break;
									}
							}
							int addAmount;
							int.TryParse(args.Parameters[3], out addAmount);
							if (TShock.Regions.resizeRegion(args.Parameters[1], addAmount, direction))
							{
								args.Player.SendMessage("Region Resized Successfully!", Color.Yellow);
								foreach (TSPlayer tsplr in TShock.Players)
								{
									if (tsplr != null && tsplr.IsRaptor && tsplr.Group.HasPermission(Permissions.manageregion))
										tsplr.SendRaptorRegion(TShock.Regions.GetRegionByName(args.Parameters[1]));
								}
								TShock.Regions.Reload();
							}
							else
								args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /region resize <region> <u/d/l/r> <amount>");
						}
						else
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /region resize <region> <u/d/l/r> <amount>");
						break;
					}
				case "tp":
					{
						if (!args.Player.Group.HasPermission(Permissions.tp))
						{
							args.Player.SendErrorMessage("You don't have the necessary permission to do that.");
							break;
						}
						if (args.Parameters.Count <= 1)
						{
							args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /region tp <region>.");
							break;
						}

						string regionName = string.Join(" ", args.Parameters.Skip(1));
						Region region = TShock.Regions.GetRegionByName(regionName);
						if (region == null)
						{
							args.Player.SendErrorMessage("Region \"{0}\" does not exist.", regionName);
							break;
						}

						args.Player.Teleport(region.Area.Center.X * 16, region.Area.Center.Y * 16);
						break;
					}
				case "help":
				default:
					{
						int pageNumber;
						int pageParamIndex = 0;
						if (args.Parameters.Count > 1)
							pageParamIndex = 1;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, pageParamIndex, args.Player, out pageNumber))
							return;

						List<string> lines = new List<string> {
                          "set <1/2> - Sets the temporary region points.",
                          "clear - Clears the temporary region points.",
                          "define <name> - Defines the region with the given name.",
                          "delete <name> - Deletes the given region.",
                          "name [-u][-z][-p] - Shows the name of the region at the given point.",
                          "list - Lists all regions.",
                          "resize <region> <u/d/l/r> <amount> - Resizes a region.",
                          "allow <user> <region> - Allows a user to a region.",
                          "remove <user> <region> - Removes a user from a region.",
                          "allowg <group> <region> - Allows a user group to a region.",
                          "removeg <group> <region> - Removes a user group from a region.",
                          "info <region> [-d] - Displays several information about the given region.",
                          "protect <name> <true/false> - Sets whether the tiles inside the region are protected or not.",
                          "z <name> <#> - Sets the z-order of the region.",
                        };
						if (args.Player.Group.HasPermission(Permissions.tp))
							lines.Add("tp <region> - Teleports you to the given region's center.");

						PaginationTools.SendPage(
						  args.Player, pageNumber, lines,
						  new PaginationTools.Settings
						  {
							  HeaderFormat = "Available Region Sub-Commands ({0}/{1}):",
							  FooterFormat = "Type /region {0} for more sub-commands."
						  }
						);
						break;
					}
			}
		}

        #endregion Region Commands

        #region World Protection Commands

        private static void ToggleAntiBuild(CommandArgs args)
		{
			TShock.Config.DisableBuild = (TShock.Config.DisableBuild == false);
			TSPlayer.All.SendSuccessMessage(string.Format("Anti-build is now {0}.", (TShock.Config.DisableBuild ? "on" : "off")));
		}

		private static void ProtectSpawn(CommandArgs args)
		{
			TShock.Config.SpawnProtection = (TShock.Config.SpawnProtection == false);
			TSPlayer.All.SendSuccessMessage(string.Format("Spawn is now {0}.", (TShock.Config.SpawnProtection ? "protected" : "open")));
		}

		#endregion World Protection Commands

		#region General Commands

		private static void Help(CommandArgs args)
		{
			if (args.Parameters.Count > 1)
			{
				args.Player.SendErrorMessage("Chybne zadani! Spravne zadani: /help <prikaz/stranka>");
				return;
			}

			int pageNumber;
			if (args.Parameters.Count == 0 || int.TryParse(args.Parameters[0], out pageNumber))
			{
				if (!PaginationTools.TryParsePageNumber(args.Parameters, 0, args.Player, out pageNumber))
				{
					return;
				}

				IEnumerable<string> cmdNames = from cmd in ChatCommands
											   where cmd.CanRun(args.Player) && (cmd.Name != "auth" || TShock.AuthToken != 0)
											   select "/" + cmd.Name;

				PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(cmdNames),
					new PaginationTools.Settings
					{
						HeaderFormat = "Dostupne prikazy ({0}/{1}):",
						FooterFormat = "Zadej /help {0} pro dalsi stranku vypisu."
					});
			}
			else
			{
				string commandName = args.Parameters[0].ToLower();
				if (commandName.StartsWith("/"))
				{
					commandName = commandName.Substring(1);
				}

				Command command = ChatCommands.Find(c => c.Names.Contains(commandName));
				if (command == null)
				{
					args.Player.SendErrorMessage("Chybny prikaz.");
					return;
				}
				if (!command.CanRun(args.Player))
				{
					args.Player.SendErrorMessage("Nemas potrebna opravneni pro tento prikaz.");
					return;
				}

				args.Player.SendSuccessMessage("/{0} help: ", command.Name);
				args.Player.SendInfoMessage(command.HelpText);
			}
		}

		private static void GetVersion(CommandArgs args)
		{
			args.Player.SendInfoMessage(string.Format("TShock: {0} ({1}): ({2}/{3})", TShock.VersionNum, TShock.VersionCodename,
												  TShock.Utils.ActivePlayers(), TShock.Config.MaxSlots));
		}

		private static void ListConnectedPlayers(CommandArgs args)
		{
			bool invalidUsage = (args.Parameters.Count > 2);

			bool displayIdsRequested = false;
			int pageNumber = 1;
			if (!invalidUsage) 
			{
				foreach (string parameter in args.Parameters)
				{
					if (parameter.Equals("-i", StringComparison.InvariantCultureIgnoreCase))
					{
						displayIdsRequested = true;
						continue;
					}

					if (!int.TryParse(parameter, out pageNumber))
					{
						invalidUsage = true;
						break;
					}
				}
			}
			if (invalidUsage)
			{
				args.Player.SendErrorMessage("Chybne pouziti prikazu. Napis: /who [-i] [cislostranky]");
				return;
			}
			if (displayIdsRequested && !args.Player.Group.HasPermission(Permissions.seeids))
			{
				args.Player.SendErrorMessage("Nemas potrebna opravneni pro vypis hracskych id.");
				return;
			}

			args.Player.SendSuccessMessage("Hraci online ({0}/{1})", TShock.Utils.ActivePlayers(), TShock.Config.MaxSlots);
			PaginationTools.SendPage(
				args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(TShock.Utils.GetPlayers(displayIdsRequested)), 
				new PaginationTools.Settings 
				{
					IncludeHeader = false,
					FooterFormat = string.Format("Zadej /who {0}{{0}} pro vice.", displayIdsRequested ? "-i " : string.Empty)
				}
			);
		}

		private static void AuthToken(CommandArgs args)
		{
			if (TShock.AuthToken == 0)
			{
				args.Player.SendWarningMessage("Auth is disabled. This incident has been logged.");
				Log.Warn(args.Player.IP + " attempted to use /auth even though it's disabled.");
				return;
			}
			int givenCode = Convert.ToInt32(args.Parameters[0]);
			if (givenCode == TShock.AuthToken && args.Player.Group.Name != "superadmin")
			{
				try
				{
					args.Player.Group = TShock.Utils.GetGroup("superadmin");
					args.Player.SendInfoMessage("You are now superadmin, please do the following to finish your install:");
					args.Player.SendInfoMessage("/user add <username> <password> superadmin");
					args.Player.SendInfoMessage("Creates: <username> with the password <password> as part of the superadmin group.");
					args.Player.SendInfoMessage("Please use /login <username> <password> to login from now on.");
					args.Player.SendInfoMessage("If you understand, please /login <username> <password> now, and type /auth-verify.");
				}
				catch (UserManagerException ex)
				{
					Log.ConsoleError(ex.ToString());
					args.Player.SendErrorMessage(ex.Message);
				}
				return;
			}

			if (args.Player.Group.Name == "superadmin")
			{
				args.Player.SendInfoMessage("Please disable the auth system! If you need help, consult the forums. http://tshock.co/");
				args.Player.SendInfoMessage("This account is superadmin, please do the following to finish your install:");
				args.Player.SendInfoMessage("Please use /login <username> <password> to login from now on.");
				args.Player.SendInfoMessage("If you understand, please /login <username> <password> now, and type /auth-verify.");
				return;
			}

			args.Player.SendErrorMessage("Incorrect auth code. This incident has been logged.");
			Log.Warn(args.Player.IP + " attempted to use an incorrect auth code.");
		}

		private static void AuthVerify(CommandArgs args)
		{
			if (TShock.AuthToken == 0)
			{
				args.Player.SendWarningMessage("It appears that you have already turned off the auth token.");
				args.Player.SendWarningMessage("If this is a mistake, delete auth.lck.");
				return;
			}

			args.Player.SendSuccessMessage("Your new account has been verified, and the /auth system has been turned off.");
			args.Player.SendSuccessMessage("You can always use the /user command to manage players. Don't just delete the auth.lck.");
			args.Player.SendSuccessMessage("Thank you for using TShock! http://tshock.co/ & http://github.com/TShock/TShock");
			FileTools.CreateFile(Path.Combine(TShock.SavePath, "auth.lck"));
			File.Delete(Path.Combine(TShock.SavePath, "authcode.txt"));
			TShock.AuthToken = 0;
		}

		private static void ThirdPerson(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /me <text>");
				return;
			}
			if (args.Player.mute)
				args.Player.SendErrorMessage("You are muted.");
			else
				TSPlayer.All.SendMessage(string.Format("*{0} {1}", args.Player.Name, String.Join(" ", args.Parameters)), 205, 133, 63);
		}

		private static void PartyChat(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /p <team chat text>");
				return;
			}
			int playerTeam = args.Player.Team;

			if (args.Player.mute)
				args.Player.SendErrorMessage("You are muted.");
			else if (playerTeam != 0)
			{
				string msg = string.Format("<{0}> {1}", args.Player.Name, String.Join(" ", args.Parameters));
				foreach (TSPlayer player in TShock.Players)
				{
					if (player != null && player.Active && player.Team == playerTeam)
						player.SendMessage(msg, Main.teamColor[playerTeam].R, Main.teamColor[playerTeam].G, Main.teamColor[playerTeam].B);
				}
			}
			else
				args.Player.SendErrorMessage("You are not in a party!");
		}

		private static void Mute(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /mute <player> [reason]");
				return;
			}

			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid player!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else if (players[0].Group.HasPermission(Permissions.mute))
			{
				args.Player.SendErrorMessage("You cannot mute this player.");
			}
			else if (players[0].mute)
			{
				var plr = players[0];
				plr.mute = false;
				TSPlayer.All.SendInfoMessage("{0} has been unmuted by {1}.", plr.Name, args.Player.Name);
			}
			else
			{
				string reason = "misbehavior";
				if (args.Parameters.Count > 1)
					reason = String.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
				var plr = players[0];
				plr.mute = true;
				TSPlayer.All.SendInfoMessage("{0} has been muted by {1} for {2}.", plr.Name, args.Player.Name, reason);
			}
		}

		private static void Motd(CommandArgs args)
		{
			TShock.Utils.ShowFileToUser(args.Player, "motd.txt");
		}

		private static void Rules(CommandArgs args)
		{
			TShock.Utils.ShowFileToUser(args.Player, "rules.txt");
		}

		private static void Whisper(CommandArgs args)
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /whisper <player> <text>");
				return;
			}

			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid player!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else if (args.Player.mute)
			{
				args.Player.SendErrorMessage("You are muted.");
			}
			else
			{
				var plr = players[0];
				var msg = string.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
				plr.SendMessage(String.Format("<From {0}> {1}", args.Player.Name, msg), Color.MediumPurple);
				args.Player.SendMessage(String.Format("<To {0}> {1}", plr.Name, msg), Color.MediumPurple);
				plr.LastWhisper = args.Player;
				args.Player.LastWhisper = plr;
			}
		}

		private static void Reply(CommandArgs args)
		{
			if (args.Player.mute)
			{
				args.Player.SendErrorMessage("You are muted.");
			}
			else if (args.Player.LastWhisper != null)
			{
				var msg = string.Join(" ", args.Parameters);
				args.Player.LastWhisper.SendMessage(String.Format("<From {0}> {1}", args.Player.Name, msg), Color.MediumPurple);
				args.Player.SendMessage(String.Format("<To {0}> {1}", args.Player.LastWhisper.Name, msg), Color.MediumPurple);
			}
			else
			{
				args.Player.SendErrorMessage("You haven't previously received any whispers. Please use /whisper to whisper to other people.");
			}
		}

		private static void Annoy(CommandArgs args)
		{
			if (args.Parameters.Count != 2)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /annoy <player> <seconds to annoy>");
				return;
			}
			int annoy = 5;
			int.TryParse(args.Parameters[1], out annoy);

			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
				args.Player.SendErrorMessage("Invalid player!");
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				var ply = players[0];
				args.Player.SendSuccessMessage("Annoying " + ply.Name + " for " + annoy + " seconds.");
				(new Thread(ply.Whoopie)).Start(annoy);
			}
		}

		private static void Confuse(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /confuse <player>");
				return;
			}
			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
				args.Player.SendErrorMessage("Invalid player!");
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				var ply = players[0];
				ply.Confused = !ply.Confused;
				args.Player.SendSuccessMessage("{0} is {1} confused.", ply.Name, ply.Confused ? "now" : "no longer");
			}
		}

		private static void Rocket(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /rocket <player>");
				return;
			}
			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
				args.Player.SendErrorMessage("Invalid player!");
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				var ply = players[0];

				if (ply.IsLoggedIn && TShock.Config.ServerSideCharacter)
				{
					ply.TPlayer.velocity.Y = -50;
					TSPlayer.All.SendData(PacketTypes.PlayerUpdate, "", ply.Index);
					args.Player.SendSuccessMessage("Rocketed {0}.", ply.Name);
				}
				else
				{
					args.Player.SendErrorMessage("Failed to rocket player: Not logged in or not SSC mode.");
				}
			}
		}

		private static void FireWork(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /firework <player> [red|green|blue|yellow]");
				return;
			}
			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
				args.Player.SendErrorMessage("Invalid player!");
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				int type = 167;
				if (args.Parameters.Count > 1)
				{
					if (args.Parameters[1].ToLower() == "green")
						type = 168;
					else if (args.Parameters[1].ToLower() == "blue")
						type = 169;
					else if (args.Parameters[1].ToLower() == "yellow")
						type = 170;
				}
				var ply = players[0];
				int p = Projectile.NewProjectile(ply.TPlayer.position.X, ply.TPlayer.position.Y - 64f, 0f, -8f, type, 0, (float)0);
				Main.projectile[p].Kill();
				args.Player.SendSuccessMessage("Launched Firework on {0}.", ply.Name);
			}
		}

		private static void Aliases(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /aliases <command or alias>");
				return;
			}
			
			string givenCommandName = string.Join(" ", args.Parameters);
			if (string.IsNullOrWhiteSpace(givenCommandName)) {
				args.Player.SendErrorMessage("Please enter a proper command name or alias.");
				return;
			}

			string commandName;
			if (givenCommandName[0] == '/')
				commandName = givenCommandName.Substring(1);
			else
				commandName = givenCommandName;

			bool didMatch = false;
			foreach (Command matchingCommand in ChatCommands.Where(cmd => cmd.Names.IndexOf(commandName) != -1)) {
				if (matchingCommand.Names.Count > 1)
					args.Player.SendInfoMessage(
					    "Aliases of /{0}: /{1}", matchingCommand.Name, string.Join(", /", matchingCommand.Names.Skip(1)));
				else
					args.Player.SendInfoMessage("/{0} defines no aliases.", matchingCommand.Name);

				didMatch = true;
			}

			if (!didMatch)
				args.Player.SendErrorMessage("No command or command alias matching \"{0}\" found.", givenCommandName);
		}

		#endregion General Commands

		#region Cheat Commands

		private static void Clear(CommandArgs args)
		{
			if (args.Parameters.Count != 1 && args.Parameters.Count != 2)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /clear <item/npc/projectile> [radius]");
				return;
			}

			int radius = 50;
			if (args.Parameters.Count == 2)
			{
				if (!int.TryParse(args.Parameters[1], out radius) || radius <= 0)
				{
					args.Player.SendErrorMessage("Invalid radius.");
					return;
				}
			}

			switch (args.Parameters[0].ToLower())
			{
				case "item":
				case "items":
					{
						int cleared = 0;
						for (int i = 0; i < Main.maxItems; i++)
						{
							float dX = Main.item[i].position.X - args.Player.X;
							float dY = Main.item[i].position.Y - args.Player.Y;

							if (Main.item[i].active && dX * dX + dY * dY <= radius * radius * 256f)
							{
								Main.item[i].active = false;
								TSPlayer.All.SendData(PacketTypes.ItemDrop, "", i);
								cleared++;
							}
						}
						args.Player.SendSuccessMessage("Deleted {0} items within a radius of {1}.", cleared, radius);
					}
					break;
				case "npc":
				case "npcs":
					{
						int cleared = 0;
						for (int i = 0; i < Main.maxNPCs; i++)
						{
							float dX = Main.npc[i].position.X - args.Player.X;
							float dY = Main.npc[i].position.Y - args.Player.Y;

							if (Main.npc[i].active && dX * dX + dY * dY <= radius * radius * 256f)
							{
								Main.npc[i].active = false;
								Main.npc[i].type = 0;
								TSPlayer.All.SendData(PacketTypes.NpcUpdate, "", i);
								cleared++;
							}
						}
						args.Player.SendSuccessMessage("Deleted {0} NPCs within a radius of {1}.", cleared, radius);
					}
					break;
				case "proj":
				case "projectile":
				case "projectiles":
					{
						int cleared = 0;
						for (int i = 0; i < Main.maxProjectiles; i++)
						{
							float dX = Main.projectile[i].position.X - args.Player.X;
							float dY = Main.projectile[i].position.Y - args.Player.Y;

							if (Main.projectile[i].active && dX * dX + dY * dY <= radius * radius * 256f)
							{
								Main.projectile[i].active = false;
								Main.projectile[i].type = 0;
								TSPlayer.All.SendData(PacketTypes.ProjectileNew, "", i);
								cleared++;
							}
						}
						args.Player.SendSuccessMessage("Deleted {0} projectiles within a radius of {1}.", cleared, radius);
					}
					break;
				default:
					args.Player.SendErrorMessage("Invalid clear option!");
					break;
			}
		}

		private static void Kill(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /kill <player>");
				return;
			}

			string plStr = String.Join(" ", args.Parameters);
			var players = TShock.Utils.FindPlayer(plStr);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid player!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else
			{
				var plr = players[0];
				plr.DamagePlayer(999999);
				args.Player.SendSuccessMessage(string.Format("You just killed {0}!", plr.Name));
				plr.SendErrorMessage(string.Format("{0} just killed you!", args.Player.Name));
			}
		}

		private static void Butcher(CommandArgs args)
		{
			if (args.Parameters.Count > 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /butcher [mob type]");
				return;
			}

			int npcId = 0;

			if (args.Parameters.Count == 1)
			{
				List<NPC> npcs = TShock.Utils.GetNPCByIdOrName(args.Parameters[0]);
				if (npcs.Count == 0)
				{
					args.Player.SendErrorMessage("Invalid mob type!");
					return;
				}
				else if (npcs.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.Player, npcs.Select(n => n.name));
					return;
				}
				else
				{
					npcId = npcs[0].netID;
				}
			}

			int kills = 0;
			for (int i = 0; i < Main.npc.Length; i++)
			{
				if (Main.npc[i].active && ((npcId == 0 && !Main.npc[i].townNPC) || Main.npc[i].netID == npcId))
				{
					TSPlayer.Server.StrikeNPC(i, 99999, 0, 0);
					kills++;
				}
			}
			TSPlayer.All.SendInfoMessage("{0} butchered {1} NPCs.", args.Player.Name, kills);
		}
		
		private static void Item(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /item <item name/id> [item amount] [prefix id/name]");
				return;
			}

			int amountParamIndex = -1;
			int itemAmount = 0;
			for (int i = 1; i < args.Parameters.Count; i++)
			{
				if (int.TryParse(args.Parameters[i], out itemAmount))
				{
					amountParamIndex = i;
					break;
				}
			}

			string itemNameOrId;
			if (amountParamIndex == -1)
				itemNameOrId = string.Join(" ", args.Parameters);
			else
				itemNameOrId = string.Join(" ", args.Parameters.Take(amountParamIndex));

			Item item;
			List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(itemNameOrId);
			if (matchedItems.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid item type!");
				return;
			}
			else if (matchedItems.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, matchedItems.Select(i => i.name));
				return;
			}
			else
			{
				item = matchedItems[0];
			}
			if (item.type < 1 && item.type >= Main.maxItemTypes)
			{
				args.Player.SendErrorMessage("The item type {0} is invalid.", itemNameOrId);
				return;
			}

			int prefixId = 0;
			if (amountParamIndex != -1 && args.Parameters.Count > amountParamIndex + 1)
			{
				string prefixidOrName = args.Parameters[amountParamIndex + 1];
				var matchedPrefixIds = TShock.Utils.GetPrefixByIdOrName(prefixidOrName);
				if (matchedPrefixIds.Count > 1) 
				{
					TShock.Utils.SendMultipleMatchError(args.Player, matchedPrefixIds.Select(p => p.ToString()));
					return;
				}
				else if (matchedPrefixIds.Count == 0) 
				{
					args.Player.SendErrorMessage("No prefix matched \"{0}\".", prefixidOrName);
					return;
				}
				else
				{
					prefixId = matchedPrefixIds[0];
				}
			}

			if (args.Player.InventorySlotAvailable || (item.type > 70 && item.type < 75) || item.ammo > 0 || item.type == 58 || item.type == 184)
			{
				if (itemAmount == 0 || itemAmount > item.maxStack)
					itemAmount = item.maxStack;

				if (args.Player.GiveItemCheck(item.type, item.name, item.width, item.height, itemAmount, prefixId))
				{
					item.prefix = (byte)prefixId;
					args.Player.SendSuccessMessage("Gave {0} {1}(s).", itemAmount, item.AffixName());
				}
				else
				{
					args.Player.SendErrorMessage("You cannot spawn banned items.");
				}
			}
			else
			{
				args.Player.SendErrorMessage("Your inventory seems full.");
			}
		}

		private static void Give(CommandArgs args)
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage(
					"Invalid syntax! Proper syntax: /give <item type/id> <player> [item amount] [prefix id/name]");
				return;
			}
			if (args.Parameters[0].Length == 0)
			{
				args.Player.SendErrorMessage("Missing item name/id.");
				return;
			}
			if (args.Parameters[1].Length == 0)
			{
				args.Player.SendErrorMessage("Missing player name.");
				return;
			}
			int itemAmount = 0;
			int prefix = 0;
			var items = TShock.Utils.GetItemByIdOrName(args.Parameters[0]);
			args.Parameters.RemoveAt(0);
			string plStr = args.Parameters[0];
			args.Parameters.RemoveAt(0);
			if (args.Parameters.Count == 1)
				int.TryParse(args.Parameters[0], out itemAmount);
			else if (args.Parameters.Count == 2)
			{
				int.TryParse(args.Parameters[0], out itemAmount);
				var found = TShock.Utils.GetPrefixByIdOrName(args.Parameters[1]);
				if (found.Count == 1)
					prefix = found[0];
			}

			if (items.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid item type!");
			}
			else if (items.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
			}
			else
			{
				var item = items[0];
				if (item.type >= 1 && item.type < Main.maxItemTypes)
				{
					var players = TShock.Utils.FindPlayer(plStr);
					if (players.Count == 0)
					{
						args.Player.SendErrorMessage("Invalid player!");
					}
					else if (players.Count > 1)
					{
						TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
					}
					else
					{
						var plr = players[0];
						if (plr.InventorySlotAvailable || (item.type > 70 && item.type < 75) || item.ammo > 0 || item.type == 58 || item.type == 184)
						{
							if (itemAmount == 0 || itemAmount > item.maxStack)
								itemAmount = item.maxStack;
							if (plr.GiveItemCheck(item.type, item.name, item.width, item.height, itemAmount, prefix))
							{
								args.Player.SendSuccessMessage(string.Format("Gave {0} {1} {2}(s).", plr.Name, itemAmount, item.name));
								plr.SendSuccessMessage(string.Format("{0} gave you {1} {2}(s).", args.Player.Name, itemAmount, item.name));
							}
							else
							{
								args.Player.SendErrorMessage("You cannot spawn banned items.");
							}
							
						}
						else
						{
							args.Player.SendErrorMessage("Player does not have free slots!");
						}
					}
				}
				else
				{
					args.Player.SendErrorMessage("Invalid item type!");
				}
			}
		}

		private static void Heal(CommandArgs args)
		{
			TSPlayer playerToHeal;
			if (args.Parameters.Count > 0)
			{
				string plStr = String.Join(" ", args.Parameters);
				var players = TShock.Utils.FindPlayer(plStr);
				if (players.Count == 0)
				{
					args.Player.SendErrorMessage("Invalid player!");
					return;
				}
				else if (players.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
					return;
				}
				else
				{
					playerToHeal = players[0];
				}
			}
			else if (!args.Player.RealPlayer)
			{
				args.Player.SendErrorMessage("You can't heal yourself!");
				return;
			}
			else
			{
				playerToHeal = args.Player;
			}

			playerToHeal.Heal();
			if (playerToHeal == args.Player)
			{
				args.Player.SendSuccessMessage("You just got healed!");
			}
			else
			{
				args.Player.SendSuccessMessage(string.Format("You just healed {0}", playerToHeal.Name));
				playerToHeal.SendSuccessMessage(string.Format("{0} just healed you!", args.Player.Name));
			}
		}

		private static void Buff(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /buff <buff id/name> [time(seconds)]");
				return;
			}
			int id = 0;
			int time = 60;
			if (!int.TryParse(args.Parameters[0], out id))
			{
				var found = TShock.Utils.GetBuffByName(args.Parameters[0]);
				if (found.Count == 0)
				{
					args.Player.SendErrorMessage("Invalid buff name!");
					return;
				}
				else if (found.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.Player, found.Select(f => Main.buffName[f]));
					return;
				}
				id = found[0];
			}
			if (args.Parameters.Count == 2)
				int.TryParse(args.Parameters[1], out time);
			if (id > 0 && id < Main.maxBuffs)
			{
				if (time < 0 || time > short.MaxValue)
					time = 60;
				args.Player.SetBuff(id, time*60);
				args.Player.SendSuccessMessage(string.Format("You have buffed yourself with {0}({1}) for {2} seconds!",
													  TShock.Utils.GetBuffName(id), TShock.Utils.GetBuffDescription(id), (time)));
			}
			else
				args.Player.SendErrorMessage("Invalid buff ID!");
		}

		private static void GBuff(CommandArgs args)
		{
			if (args.Parameters.Count < 2 || args.Parameters.Count > 3)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /gbuff <player> <buff id/name> [time(seconds)]");
				return;
			}
			int id = 0;
			int time = 60;
			var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (foundplr.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid player!");
				return;
			}
			else if (foundplr.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
				return;
			}
			else
			{
				if (!int.TryParse(args.Parameters[1], out id))
				{
					var found = TShock.Utils.GetBuffByName(args.Parameters[1]);
					if (found.Count == 0)
					{
						args.Player.SendErrorMessage("Invalid buff name!");
						return;
					}
					else if (found.Count > 1)
					{
						TShock.Utils.SendMultipleMatchError(args.Player, found.Select(b => Main.buffName[b]));
						return;
					}
					id = found[0];
				}
				if (args.Parameters.Count == 3)
					int.TryParse(args.Parameters[2], out time);
				if (id > 0 && id < Main.maxBuffs)
				{
					if (time < 0 || time > short.MaxValue)
						time = 60;
					foundplr[0].SetBuff(id, time*60);
					args.Player.SendSuccessMessage(string.Format("You have buffed {0} with {1}({2}) for {3} seconds!",
														  foundplr[0].Name, TShock.Utils.GetBuffName(id),
														  TShock.Utils.GetBuffDescription(id), (time)));
					foundplr[0].SendSuccessMessage(string.Format("{0} has buffed you with {1}({2}) for {3} seconds!",
														  args.Player.Name, TShock.Utils.GetBuffName(id),
														  TShock.Utils.GetBuffDescription(id), (time)));
				}
				else
					args.Player.SendErrorMessage("Invalid buff ID!");
			}
		}

		private static void Grow(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /grow <tree/epictree/mushroom/cactus/herb>");
				return;
			}
			var name = "Fail";
			var x = args.Player.TileX;
			var y = args.Player.TileY + 3;
			switch (args.Parameters[0].ToLower())
			{
				case "tree":
					for (int i = x - 1; i < x + 2; i++)
					{
						Main.tile[i, y].active(true);
						Main.tile[i, y].type = 2;
						Main.tile[i, y].wall = 0;
					}
					Main.tile[x, y - 1].wall = 0;
					WorldGen.GrowTree(x, y);
					name = "Tree";
					break;
				case "epictree":
					for (int i = x - 1; i < x + 2; i++)
					{
						Main.tile[i, y].active(true);
						Main.tile[i, y].type = 2;
						Main.tile[i, y].wall = 0;
					}
					Main.tile[x, y - 1].wall = 0;
					Main.tile[x, y - 1].liquid = 0;
					Main.tile[x, y - 1].active(true);
					WorldGen.GrowEpicTree(x, y);
					name = "Epic Tree";
					break;
				case "mushroom":
					for (int i = x - 1; i < x + 2; i++)
					{
						Main.tile[i, y].active(true);
						Main.tile[i, y].type = 70;
						Main.tile[i, y].wall = 0;
					}
					Main.tile[x, y - 1].wall = 0;
					WorldGen.GrowShroom(x, y);
					name = "Mushroom";
					break;
				case "cactus":
					Main.tile[x, y].type = 53;
					WorldGen.GrowCactus(x, y);
					name = "Cactus";
					break;
				case "herb":
					Main.tile[x, y].active(true);
					Main.tile[x, y].frameX = 36;
					Main.tile[x, y].type = 83;
					WorldGen.GrowAlch(x, y);
					name = "Herb";
					break;
				default:
					args.Player.SendErrorMessage("Unknown plant!");
					return;
			}
			args.Player.SendTileSquare(x, y);
			args.Player.SendSuccessMessage("Tried to grow a " + name + ".");
		}

		private static void ToggleGodMode(CommandArgs args)
		{
			TSPlayer playerToGod;
			if (args.Parameters.Count > 0)
			{
				string plStr = String.Join(" ", args.Parameters);
				var players = TShock.Utils.FindPlayer(plStr);
				if (players.Count == 0)
				{
					args.Player.SendErrorMessage("Invalid player!");
					return;
				}
				else if (players.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
					return;
				}
				else
				{
					playerToGod = players[0];
				}
			}
			else if (!args.Player.RealPlayer)
			{
				args.Player.SendErrorMessage("You can't god mode a non player!");
				return;
			}
			else
			{
				playerToGod = args.Player;
			}

			playerToGod.GodMode = !playerToGod.GodMode;

			if (playerToGod == args.Player)
			{
				args.Player.SendSuccessMessage(string.Format("You are {0} in god mode.", args.Player.GodMode ? "now" : "no longer"));
			}
			else
			{
				args.Player.SendSuccessMessage(string.Format("{0} is {1} in god mode.", playerToGod.Name, playerToGod.GodMode ? "now" : "no longer"));
				playerToGod.SendSuccessMessage(string.Format("You are {0} in god mode.", playerToGod.GodMode ? "now" : "no longer"));
			}
		}

		#endregion Cheat Comamnds
	}
}
