using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Serialization;
using UnityEngine.UIElements;

public class MainMenuManager : Singleton<MainMenuManager>, IUISetup
{
    [DontSerialize] public string? ConnectionError;

    public void Setup(UIElementReference ui)
    {
        ui.Element.Q<Button>("button-singleplayer").clicked += () =>
        {
            if (!HandleInput(ui, out _, out FixedString32Bytes nickname)) return;
            ConnectionManager.Instance.StartCoroutine(ConnectionManager.Instance.StartSingleplayer(nickname, null));
        };
        ui.Element.Q<Button>("button-host").clicked += () =>
        {
            if (!HandleInput(ui, out NetworkEndpoint endpoint, out FixedString32Bytes nickname)) return;
            ConnectionManager.Instance.StartCoroutine(ConnectionManager.Instance.StartHost(endpoint, nickname, null));
        };
        ui.Element.Q<Button>("button-client").clicked += () =>
        {
            if (!HandleInput(ui, out NetworkEndpoint endpoint, out FixedString32Bytes nickname)) return;
            ConnectionManager.Instance.StartCoroutine(ConnectionManager.Instance.StartClient(endpoint, nickname));
        };
        ui.Element.Q<Button>("button-server").clicked += () =>
        {
            if (!HandleInput(ui, out NetworkEndpoint endpoint, out _)) return;
            ConnectionManager.Instance.StartCoroutine(ConnectionManager.Instance.StartServer(endpoint, null));
        };
        ui.Element.Q<Button>("button-staging").clicked += () =>
        {
            if (!HandleInput(ui, out _, out FixedString32Bytes nickname)) return;
            ConnectionManager.Instance.StartCoroutine(ConnectionManager.Instance.StartStaging(nickname, null));
        };
        ui.Element.Q<Button>("button-exit").clicked += UnityUtils.Quit;

        if (ConnectionError is not null)
        {
            ui.Element.Q<Label>("error-connection").text = ConnectionError;
            ui.Element.Q<Label>("error-connection").style.display = DisplayStyle.Flex;
            ConnectionError = null;
        }
        else
        {
            ui.Element.Q<Label>("error-connection").text = "";
            ui.Element.Q<Label>("error-connection").style.display = DisplayStyle.None;
        }

        ui.Element.Q<Label>("input-error-host").text = "";
        ui.Element.Q<Label>("input-error-host").style.display = DisplayStyle.None;

        ui.Element.Q<Label>("input-error-nickname").text = "";
        ui.Element.Q<Label>("input-error-nickname").style.display = DisplayStyle.None;
    }

    bool HandleInput(UIElementReference ui, [NotNullWhen(true)] out NetworkEndpoint endpoint, out FixedString32Bytes nickname)
    {
        bool ok = true;

        Label inputErrorLabel = ui.Element.Q<Label>("input-error-host");
        inputErrorLabel.style.display = DisplayStyle.None;

        string inputNickname = ui.Element.Q<TextField>("input-nickname").value.Trim();

        if (inputNickname.Length >= FixedString32Bytes.UTF8MaxLengthInBytes)
        {
            inputErrorLabel.text = "Too long nickname";
            inputErrorLabel.style.display = DisplayStyle.Flex;
            ok = false;
        }
        else if (string.IsNullOrEmpty(inputNickname))
        {
            inputErrorLabel.text = "Empty nickname";
            inputErrorLabel.style.display = DisplayStyle.Flex;
            ok = false;
        }

        nickname = inputNickname;

        string inputHost = ui.Element.Q<TextField>("input-host").value;
        if (!ParseInput(inputHost, out endpoint, out string? inputErrorHost))
        {
            inputErrorLabel.text = inputErrorHost;
            inputErrorLabel.style.display = DisplayStyle.Flex;
            ok = false;
        }
        if (ok)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    static bool ParseInput(string input, [NotNullWhen(true)] out NetworkEndpoint endpoint, [NotNullWhen(false)] out string? error)
    {
        if (!input.Contains(':'))
        {
            error = $"Invalid host input";
            endpoint = default;
            return false;
        }

        if (!ushort.TryParse(input.Split(':')[1], out ushort port))
        {
            error = $"Invalid host input";
            endpoint = default;
            return false;
        }

        if (!NetworkEndpoint.TryParse(input.Split(':')[0], port, out endpoint))
        {
            error = $"Invalid host input";
            return false;
        }

        error = null;
        return true;
    }
}
