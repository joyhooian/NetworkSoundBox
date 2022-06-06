using System;
using System.Collections.Generic;

namespace NetworkSoundBox.Services.Message
{
    public class MessageContext : IMessageContext
    {
        private readonly Dictionary<Command, MessageToken> _messageTokens = new();

        public MessageToken GetToken(Command command)
            => _messageTokens.ContainsKey(command) ? _messageTokens[command] : null;

        public void SetToken(MessageToken token)
        {
            lock(_messageTokens){
                if (_messageTokens.TryGetValue(token.ExpRplCmd, out var t))
                {
                    t.Status = MessageStatus.Canceled;
                    _messageTokens.Remove(t.ExpRplCmd);
                }

                _messageTokens.Add(token.ExpRplCmd, token);
            }
        }
    }
}