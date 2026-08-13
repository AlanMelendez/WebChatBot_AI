using Microsoft.Extensions.AI;

namespace BlazorAI.DTOs
{
    public class ApprovalRequestUI
    {
        public required ToolApprovalRequestContent ApprovalRequestContent { get; set; }
        public required string ToolName { get; set; }

        public Dictionary<string,object>? Arguments { get; set; } // A dictionary to hold the arguments for the tool function, where the key is the argument name and the value is the argument value. This allows for dynamic passing of parameters to the tool function.

    }
}
