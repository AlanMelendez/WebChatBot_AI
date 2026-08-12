using BlazorAI.DTOs;
using Microsoft.Extensions.AI;

namespace BlazorAI.Services
{
    public class RealChatBot : IChatbot
    {
        private readonly IChatClient _chatClient; // The chat client used to send and receive messages from the AI model.
        private readonly List<ChatMessage> _messages = []; // A list to store the conversation messages exchanged with the AI model.
        public List<ChatMessageUI> Conversation { get; } = []; // A list to store the conversation messages in a format suitable for UI display.


        public RealChatBot(IChatClient chatClient)
        {
           this._chatClient = chatClient;


            var systemPrompt = """
                    You are an assistant that answers general questions.
                    You must respond in English.
                    Responses should be concise unless instructed otherwise.
                    Responses must be in plain text; do not use formats such as Markdown.
                    If a tool fails, read the exception message to see if you can fix it by making an adjustment. Inform the user of any adjustment you are going to make.
                    """;

            _messages.Add(new ChatMessage(ChatRole.System, systemPrompt)); // Add the system prompt to the conversation history to set the context for the AI model.
        }





        public bool IsProcessing { get; private set; }

        public event Action? OnChange;

        public void CancelCurrentResponse()
        {
            throw new NotImplementedException();
        }

        public Task ResolveApprovalAsync(bool approved, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task SendMessageAsync(string userText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userText)) return;

            if (IsProcessing) return;

            IsProcessing = true;

            Conversation.Add(new ChatMessageUI { Role = DTOs.MessageRole.User, Text = userText }); // Add the user's message to the conversation history for UI display.
            _messages.Add(new ChatMessage(ChatRole.User, userText)); // Add the user's message to the conversation history for AI processing.
            Conversation.Add(new ChatMessageUI { Role = DTOs.MessageRole.AI, Text = string.Empty }); // Add a placeholder message to indicate that the AI is processing the user's input.

            await SendMessagesToTheAssistant(cancellationToken); // Send the conversation messages to the AI model and process the response.
            IsProcessing = false;
        }
         
        private async Task SendMessagesToTheAssistant(CancellationToken cancellationToken = default)
        {
            var updates = new List<ChatResponseUpdate>(); // A list to store updates to the conversation messages for UI display.

            await foreach (var update in _chatClient.GetStreamingResponseAsync(_messages, cancellationToken: cancellationToken))
            {
                updates.Add(update);

                if(update.Contents.Count > 0)
                {
                    foreach(var content in update.Contents)
                    {
                    
                        if (content is TextContent textContent)
                        {
                            Conversation[^1].Text += textContent.Text; // Get the last message in the conversation (the AI's response) and append the new text content to it for UI display.
                            NotifyStateChange(); // Notify subscribers that the state has changed, prompting a UI update.
                        }
                    }
                }
            };

            var response = updates.ToChatResponse(); // Convert the list of updates to a single chat response for processing.
            _messages.AddMessages(response);
        }

        private void NotifyStateChange() => OnChange?.Invoke(); // Notify subscribers that the state has changed, prompting a UI update.
    }

}
