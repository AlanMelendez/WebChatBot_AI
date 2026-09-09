using BlazorAI.DTOs;
using Microsoft.Extensions.AI;

namespace BlazorAI.Services.RAG
{
    public class ChatbotRAG : IChatbot
    {
        private CancellationTokenSource cancellation;
        private readonly IChatClient _chatClient; // The chat client used to send and receive messages from the AI model.
        private readonly List<ChatMessage> _messages = []; // A list to store the conversation messages exchanged with the AI model.
        public List<ChatMessageUI> Conversation { get; } = []; // A list to store the conversation messages in a format suitable for UI display.

        private readonly ChatOptions _chatOptions; // The chat options used to configure the behavior of the AI model.

        private readonly Queue<ToolApprovalRequestContent> pendingApprovals = new();

        private readonly IRAGService _ragService; // The RAG service used to find relevant context for the user's queries.

        public ChatbotRAG(IChatClient chatClient, ChatOptions chatOptions, IRAGService _ragService)
        {
            this._chatClient = chatClient;
            this._chatOptions = chatOptions;
            this._ragService = _ragService;


            var systemPrompt = """
                    You are an assistant specialized exclusively in answering questions using the context retrieved from internal documents.

                    You must respond in English.
                    Responses must be in plain text, without markdown.

                    Mandatory rules:

                    - Answer only with information contained in the retrieved context.
                    - If the answer is not explicitly in the context, you must respond: "I do not have sufficient information in the documents to answer that question."
                    - Do not use the model’s general knowledge.
                    - Do not invent information.
                    - Do not answer questions about programming, general knowledge, mathematics, or other topics if they do not appear in the retrieved context.
                    - If the question is not related to the documents, reject it briefly.
                    """;

            _messages.Add(new ChatMessage(ChatRole.System, systemPrompt)); // Add the system prompt to the conversation history to set the context for the AI model.
        }



        public bool IsProcessing { get; private set; }

        public ApprovalRequestUI? PendingApproval { get; private set; }

        public event Action? OnChange;

        public void CancelCurrentResponse()
        {
            if (IsProcessing)
            {
                cancellation.Cancel();

            }
        }

        public  Task ResolveApprovalAsync(bool approved, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;


        }
        private void HandleOperationCanceled()
        {
            if (Conversation.Count > 0 && Conversation[^1].Role == DTOs.MessageRole.AI)
            {

                if (string.IsNullOrWhiteSpace(Conversation[^1].Text))
                {
                    Conversation[^1].Text = "[Canceled response]";
                }
                else
                {
                    Conversation[^1].Text = "[canceled]";

                }
            }

        }

        private void HandleFinally()
        {
            IsProcessing = false;
            cancellation?.Dispose();
            cancellation = null;

            NotifyStateChange();
        }

        public async Task SendMessageAsync(string userText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userText)) return;
            if (IsProcessing || PendingApproval != null) return;

            try
            {
                IsProcessing = true;

                Conversation.Add(new ChatMessageUI { Role = DTOs.MessageRole.User, Text = userText }); // Add the user's message to the conversation history for UI display.
                _messages.Add(new ChatMessage(ChatRole.User, userText)); // Add the user's message to the conversation history for AI processing.
                Conversation.Add(new ChatMessageUI { Role = DTOs.MessageRole.AI, Text = string.Empty }); // Add a placeholder message to indicate that the AI is processing the user's input.

                await SendMessagesToTheAssistant(userText, cancellationToken); // Send the conversation messages to the AI model and process the response.
            }
            catch (OperationCanceledException ex)
            {
                HandleOperationCanceled();
            }
            finally
            {
                HandleFinally();
            }

        }

        private async Task SendMessagesToTheAssistant(string userPrompt,CancellationToken cancellationToken = default)
        {

            var context = await _ragService.FindRelevantContext(userPrompt, 3, 0.6f, cancellationToken); // Find relevant context for the user's input using the RAG service.


            if(context.Count == 0)
            {
                Conversation[^1].Text = "I do not have sufficient information in the documents to answer that question."; // If no relevant context is found, respond with a message indicating that there is insufficient information to answer the question.
                NotifyStateChange(); // Notify subscribers that the state has changed, prompting a UI update.
                return;
            }

             /* messageContext structure:

                Document: Document-01
                Content: Content of the first document

                ---

                Document: Document-02
                Content: Content of the second document

                ---
                Document: Document-03
                Content: Content of the third document
             */
            var messageContext = new ChatMessage(ChatRole.System, $$"""

                Context recovered from the documents:
                {{string.Join("\n\n---\n\n", context)}}

                Question from the user: {{userPrompt}}

                Instructions:
                - Answer only with information contained in the retrieved context.
                - If the answer is not explicitly in the context, you must respond: "I do not have sufficient information in the documents to answer that question."


            """); // Create a system message containing the relevant context for the AI model.


            var messagesToSend = new List<ChatMessage>();
            messagesToSend.AddRange(_messages); // Add the existing conversation messages to the list of messages to send to the AI model.
            messagesToSend.Insert(_messages.Count - 1, messageContext); // Insert the relevant context message before the last message (the user's input) in the list of messages to send to the AI model.



            var updates = new List<ChatResponseUpdate>(); // A list to store updates to the conversation messages for UI display.

            await foreach (var update in _chatClient.GetStreamingResponseAsync(messagesToSend, _chatOptions, cancellationToken: cancellationToken))
            {
                updates.Add(update);

                if (update.Contents.Count > 0)
                {
                    foreach (var content in update.Contents)
                    {

                        if (content is TextContent textContent)
                        {
                            Conversation[^1].Text += textContent.Text; // Get the last message in the conversation (the AI's response) and append the new text content to it for UI display.
                            NotifyStateChange(); // Notify subscribers that the state has changed, prompting a UI update.
                        }
                    }
                }
            }
            ;

            var response = updates.ToChatResponse(); // Convert the list of updates to a single chat response for processing.
            _messages.AddMessages(response);


        }

        private void NotifyStateChange() => OnChange?.Invoke(); // Notify subscribers that the state has changed, prompting a UI update.
    }
}
