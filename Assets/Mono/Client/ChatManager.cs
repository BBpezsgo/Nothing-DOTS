using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class ChatManager : Singleton<ChatManager>
{
    readonly struct ChatMessage
    {
        public readonly int Sender;
        public readonly string Message;
        public readonly DateTimeOffset Time;

        public ChatMessage(int sender, string message, DateTimeOffset time)
        {
            Sender = sender;
            Message = message;
            Time = time;
        }
    }

    [SerializeField, NotNull] UIDocument? _ui = default;

    [SerializeField, NotNull] VisualTreeAsset? _chatMessageTemplate = default;

    [NotNull] TextField? _inputMessage = default;
    [NotNull] Button? _buttonSend = default;
    [NotNull] VisualElement? _containerMessages = default;
    [NotNull] VisualElement? _containerInput = default;
    readonly List<ChatMessage> _chatMessages = new();

    void Start()
    {
        _inputMessage = _ui.rootVisualElement.Q<TextField>("input-message");
        _buttonSend = _ui.rootVisualElement.Q<Button>("button-send");
        _containerMessages = _ui.rootVisualElement.Q<VisualElement>("container-messages");
        _containerInput = _ui.rootVisualElement.Q<VisualElement>("container-input");

        _buttonSend.clicked += OnButtonSend;

        _containerMessages.Clear();
        _containerInput.style.display = DisplayStyle.None;
    }

    void Update()
    {
        if (_containerMessages.childCount > 0)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (VisualElement child in _containerMessages.Children())
            {
                ChatMessage message = (ChatMessage)child.userData;
                child.EnableInClassList("old", (now - message.Time).TotalSeconds > 3);
                child.EnableInClassList("very-old", (now - message.Time).TotalSeconds > 4);
            }
        }

        if (!Input.GetKeyDown(KeyCode.Return) || UIManager.Instance.AnyUIVisible || SelectionManager.Instance.IsUnitCommandsActive) return;

        if (_containerInput.style.display == DisplayStyle.None)
        {
            _containerInput.style.display = DisplayStyle.Flex;
            _inputMessage.Focus();
            _containerMessages.EnableInClassList("show", true);
        }
        else
        {
            OnButtonSend();
            _containerInput.style.display = DisplayStyle.None;
            _containerMessages.EnableInClassList("show", false);
        }
    }

    void OnButtonSend()
    {
        string message = _inputMessage.value.Trim();
        if (string.IsNullOrWhiteSpace(message)) return;
        SendChatMessage(message);
        _inputMessage.value = string.Empty;
    }

    public void AppendChatMessageElement(int sender, string message)
    {
        _chatMessages.Add(new ChatMessage(sender, message, DateTimeOffset.UtcNow));
        RefreshChatContainer();
    }

    void RefreshChatContainer()
    {
        _containerMessages.SyncList(_chatMessages, _chatMessageTemplate, (item, element, reuse) =>
        {
            element.EnableInClassList("server-message", item.Sender == 0);
            element.EnableInClassList("system-message", item.Sender == -1);
            element.EnableInClassList("player-message", item.Sender > 0);
            element.userData = item;

            if (item.Sender > 0)
            {
                EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
                using EntityQuery playersQ = entityManager.CreateEntityQuery(typeof(Player));
                using NativeArray<Player> players = playersQ.ToComponentDataArray<Player>(Allocator.Temp);

                string? senderDisplayName = null;

                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i].ConnectionId != item.Sender) continue;
                    senderDisplayName = players[i].Nickname.ToString();
                    break;
                }

                senderDisplayName ??= $"Client#{item.Sender}";

                element.Q<Label>("label-message").text = $"<{senderDisplayName}> {item.Message}";
            }
            else
            {
                element.Q<Label>("label-message").text = item.Message;
            }
        });
    }

    void SendChatMessage(string message)
    {
        NetcodeUtils.CreateRPC(ConnectionManager.ClientOrDefaultWorld.Unmanaged, new ChatMessageRpc()
        {
            Sender = 0,
            Message = message,
        });
    }
}
