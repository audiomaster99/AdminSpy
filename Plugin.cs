namespace AdminSpy
{
	using System.Text.Json.Serialization;
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Attributes.Registration;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;
	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("admin-permission-flags")]
		public List<string> AdminFlags { get; set; } = new List<string>
		{
			"@css/chat",
			"#css/admin"
		};

		[JsonPropertyName("chat-spy-enabled")]
		public bool ChatSpyEnabled { get; set; } = true;

		[JsonPropertyName("voice-spy-enabled")]
		public bool VoiceSpyEnabled { get; set; } = true;

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 1;
	}
	public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
	{
		public override string ModuleName => "AdminSpy";
		public override string ModuleAuthor => "audio_brutalci";
		public override string ModuleDescription => "Plugin that lets admins hear and see opponents chat";
		public override string ModuleVersion => "V. 1.0.0";

		public required PluginConfig Config { get; set; } = new PluginConfig();
		public void OnConfigParsed(PluginConfig config)
		{
			if (config.Version < Config.Version)
			{
				base.Logger.LogWarning("The plugin configuration is outdated. Please consider updating the configuration file. [Expected: {0} | Current: {1}]", this.Config.Version, config.Version);
			}

			this.Config = config;
		}

		public override void Load(bool hotReload)
		{
			AddCommandListener("say_team", OnPlayerTeamChat);
			RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
		}

		public HookResult OnPlayerTeamChat(CCSPlayerController? player, CommandInfo info)
		{
			if (!Config.ChatSpyEnabled)
				return HookResult.Continue;

			if (player == null || info.GetArg(1).Length == 0)
				return HookResult.Continue;

			string playerMessage = info.GetArg(1);
			if (playerMessage == null || playerMessage == "")
				return HookResult.Continue;

			string startsWith = playerMessage.Substring(0, 1);
			if (startsWith == "/" || startsWith == "!" || startsWith == "@")
				return HookResult.Continue;

			string? PlayerTeam = GetPlayerTeamColor(player.Team);
			string? isAlive = player.PawnIsAlive ? "" : $"{Localizer["player.isdead"]}";
			string? tag = $"{Localizer["startline.tag"]}";
			
			var messageFormatted = $" {Localizer["message.formatted", tag, PlayerTeam, player.PlayerName, isAlive]} {ChatColors.Default}: {playerMessage}";

			foreach (var adminPlayer in Utilities.GetPlayers().Where(player => ClientIsValid(player) && ClientHasPermissions(player)))
			{
				if (adminPlayer == player) continue;
				if (adminPlayer.Team == player.Team) continue;

				adminPlayer.PrintToChat(messageFormatted);
			}
			return HookResult.Continue;
		}

		public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
		{
			CCSPlayerController player = @event.Userid;

			if (player is null)
				return HookResult.Continue;

			if (!Config.VoiceSpyEnabled)
				return HookResult.Continue;

			if (player?.IsValid == true)
			{
				if (ClientHasPermissions(player))
				{
					player.VoiceFlags = VoiceFlags.ListenAll;
				}
				else player.VoiceFlags = VoiceFlags.ListenTeam;
			}
			return HookResult.Continue;
		}

		[ConsoleCommand("css_voicespy", "Toggles voice spy for admins")]
		public void OnVoiceSpyCommand(CCSPlayerController? player, CommandInfo command)
		{
			if (player is null || !player.IsValid || !ClientHasPermissions(player))
				return;

			if (player.VoiceFlags.HasFlag(VoiceFlags.ListenAll))
			{
				player.VoiceFlags = VoiceFlags.ListenTeam;
				command.ReplyToCommand($" {Localizer["startline.tag"]} {ChatColors.Default}Your voice settings have been set to {ChatColors.Green}default ");
			}
			else
			{
				player.VoiceFlags = VoiceFlags.ListenAll;
				command.ReplyToCommand($" {Localizer["startline.tag"]} {ChatColors.Default}Your voice settings have been set to {ChatColors.Green}listen all ");
			}
		}

		private string GetPlayerTeamColor(CsTeam? csTeam)
		{
			string playerTeamColor;
			switch (csTeam)
			{
				case CsTeam.Terrorist:
					playerTeamColor = $"{Localizer["team.terrorist"]}";
					break;
				case CsTeam.CounterTerrorist:
					playerTeamColor = $"{Localizer["team.counterterrorist"]}";
					break;
				default:
					playerTeamColor = $"{Localizer["team.spectator"]}";
					break;
			}
			return playerTeamColor;
		}

		public bool ClientHasPermissions(CCSPlayerController player)
		{
			bool isAdmin = false;

			foreach (string adminFlags in Config.AdminFlags)
			{
				switch (adminFlags[0])
				{
					case '@':
						if (AdminManager.PlayerHasPermissions(player, adminFlags))
							isAdmin = true;
						break;
					case '#':
						if (AdminManager.PlayerInGroup(player, adminFlags))
							isAdmin = true;
						break;
					default:
						if (AdminManager.PlayerHasCommandOverride(player, adminFlags))
							isAdmin = true;
						break;
				}
			}
			return isAdmin;
		}

		static bool ClientIsValid(CCSPlayerController? player)
		{
			return player?.IsValid == true && player.PlayerPawn?.IsValid == true && !player.IsBot && !player.IsHLTV && player?.Connected == PlayerConnectedState.PlayerConnected;
		}
	}
}