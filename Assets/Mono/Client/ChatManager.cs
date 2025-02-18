using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

public class ChatManager : Singleton<ChatManager>
{
    [SerializeField, NotNull] UIDocument? _ui = default;

    [SerializeField, NotNull] VisualTreeAsset? _chatMessageTemplate = default;

    [NotNull] TextField? _inputMessage = default;
    [NotNull] Button? _buttonSend = default;
    [NotNull] VisualElement? _chatMessagesContainer = default;

    void Start()
    {
        _inputMessage = _ui.rootVisualElement.Q<TextField>("input-message");
        _buttonSend = _ui.rootVisualElement.Q<Button>("button-send");
        _chatMessagesContainer = _ui.rootVisualElement.Q<VisualElement>("container-messages");

        _buttonSend.clicked += OnButtonSend;

        _chatMessagesContainer.Clear();
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
        var instance = _chatMessageTemplate.Instantiate();
        instance.Q<Label>("label-message").text = $"<{sender}>: {message}";

        if (sender == 0)
        {
            instance.AddToClassList("server-message");
        }
        else
        {
            instance.AddToClassList("player-message");
        }

        _chatMessagesContainer.Add(instance);
    }

    void SendChatMessage(string message)
    {
        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        Entity entity = entityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(ChatMessageRpc));
        entityManager.SetComponentData(entity, new ChatMessageRpc()
        {
            Sender = 0,
            Message = message,
        });
    }
}
