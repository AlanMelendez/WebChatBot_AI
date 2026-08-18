using BlazorAI.DTOs;
using Microsoft.Extensions.AI;

namespace BlazorAI.Services
{

    
    public class RealChatBot : IChatbot
    {
        private readonly IChatClient _chatClient; // The chat client used to send and receive messages from the AI model.
        private readonly List<ChatMessage> _messages = []; // A list to store the conversation messages exchanged with the AI model.
        public List<ChatMessageUI> Conversation { get; } = []; // A list to store the conversation messages in a format suitable for UI display.

        private readonly ChatOptions _chatOptions; // The chat options used to configure the behavior of the AI model.

        public RealChatBot(IChatClient chatClient, ChatOptions chatOptions)
        {
           this._chatClient = chatClient;
           this._chatOptions = chatOptions;


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

        public ApprovalRequestUI? PendingApproval { get; private set; }

        public event Action? OnChange;

        public void CancelCurrentResponse()
        {
            throw new NotImplementedException();
        }

        public async Task ResolveApprovalAsync(bool approved, CancellationToken cancellationToken = default)
        {
            if (IsProcessing || PendingApproval is null) return;


            IsProcessing = true;

            var approvalResponse = PendingApproval.ApprovalRequestContent.CreateResponse(approved); // Create a response to the approval request based on whether the user approved or denied it.

            _messages.Add(new ChatMessage(ChatRole.User, [approvalResponse])); // Add the user's response to the approval request to the conversation history for AI processing.


            Conversation.Add(new ChatMessageUI { Role = DTOs.MessageRole.User, Text = approved ? "Action approved by the user" : "Action denied by the user" }); // Add the user's response to the approval request to the conversation history for UI display.
        
            Conversation.Add(new ChatMessageUI { Role = DTOs.MessageRole.AI, Text = string.Empty }); // Add a placeholder message to indicate that the AI is processing the user's response to the approval request.

            PendingApproval = null; // Clear the pending approval request since it has been resolved.

            NotifyStateChange(); // Notify subscribers that the state has changed, prompting a UI update.

            await SendMessagesToTheAssistant(cancellationToken); // Send the conversation messages to the AI model and process the response.
             
            IsProcessing = false;
        }

        public async Task SendMessageAsync(string userText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userText)) return;

            if (IsProcessing || PendingApproval != null) return;

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

            await foreach (var update in _chatClient.GetStreamingResponseAsync(_messages,_chatOptions, cancellationToken: cancellationToken))
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



            var approvalRequest = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<ToolApprovalRequestContent>()
                .FirstOrDefault(); // Check if the AI's response contains a tool approval request.

            if (approvalRequest != null)
            {
                if (approvalRequest.ToolCall is FunctionCallContent functionCalL) // Check if the tool approval request is a function call.
                {

                    // Create a new approval request UI object with the tool approval request details for UI display.
                    PendingApproval = new ApprovalRequestUI
                    {
                        ApprovalRequestContent = approvalRequest,
                        ToolName = ConvertToolName(functionCalL.Name),
                        Arguments = functionCalL?.Arguments?.ToDictionary(x => x.Key, x => x.Value) ?? [] //
                    }; 
                }


                //Remove the empty AI message that was added as a placeholder for the approval request.
                if(string.IsNullOrWhiteSpace(Conversation[^1].Text))
                {
                    Conversation.RemoveAt(Conversation.Count - 1);
                }
                NotifyStateChange();
                return;
            }
            else
            {
                // If there is no approval request, update the last AI message with the final response text.
                var finalResponseText = string.Join("", response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<TextContent>()
                    .Select(tc => tc.Text));
                Conversation[^1].Text = finalResponseText; // Update the last AI message with the final response text for UI display.
                NotifyStateChange(); // Notify subscribers that the state has changed, prompting a UI update.
            }

        }

        private static string ConvertToolName(string toolName)
        {
            return toolName switch
            {
                "SendEmail" => "Send email",
                _ => toolName
            };
        }

        private void NotifyStateChange() => OnChange?.Invoke(); // Notify subscribers that the state has changed, prompting a UI update.
    }

}
