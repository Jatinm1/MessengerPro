using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp.Domain.Chat
{
    public record DeleteMessageRequest(
    long MessageId,
    bool DeleteForEveryone
);

    public record EditMessageRequest(
        long MessageId,
        string NewBody
    );

    public record ForwardMessageRequest(
        long MessageId,
        Guid TargetConversationId
    );

    public record MessageActionResponse(
        bool Success,
        string? ErrorMessage
    );
}
