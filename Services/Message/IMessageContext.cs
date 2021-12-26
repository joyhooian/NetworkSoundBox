namespace NetworkSoundBox.Services.Message
{
    public interface IMessageContext
    {
        MessageToken GetToken(Command command);
        void SetToken(MessageToken token);
    }
}