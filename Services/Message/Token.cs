using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Message
{
    public class Token
    {
        public readonly List<Token> MessageTokenList;
        public byte[] Data { get; set; }
        private readonly Semaphore _semaphore;
        public Command ExpectCommand { get; }
        public Command ExceptionCommand { get; }
        private MessageStatus _status;
        public MessageStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                if (_semaphore != null)
                {
                    _semaphore.Release();
                    if (_status >= MessageStatus.Replied && MessageTokenList.Contains(this))
                        MessageTokenList.Remove(this);
                }
            }
        }
        private readonly byte[] _expectReply;

        /// <summary>
        /// Token构造
        /// </summary>
        /// <param name="tokenList">Token列表</param>
        /// <param name="expectCommand">期待的回复命令</param>
        /// <param name="exceptionCommand">错误的回复命令</param>
        /// <param name="expectReply">期待的回复内容</param>
        public Token(List<Token> tokenList, Command? expectCommand, Command? exceptionCommand, byte[] expectReply = null)
        {
            MessageTokenList = tokenList;
            _status = MessageStatus.Untouched;
            _semaphore = new Semaphore(0, 3);
            ExpectCommand = expectCommand ?? Command.None;
            ExceptionCommand = exceptionCommand ?? Command.None;
            _expectReply = expectReply;
            if (ExceptionCommand != Command.None || ExpectCommand != Command.None)
                MessageTokenList.Add(this);
        }

        /// <summary>
        /// 检查回复内容
        /// </summary>
        /// <param name="expectReply">期待的回复内容</param>
        /// <returns></returns>
        public bool CheckReply(byte[] expectReply = null)
        {
            expectReply ??= _expectReply;
            if (Data == null || expectReply == null) return false;

            if (Data.Length != expectReply.Length) return false;

            for (int i = 0; i < Data.Length; i++)
                if (Data[i] != expectReply[i]) return false;

            return true;
        }

        /// <summary>
        /// 等待发送成功
        /// </summary>
        /// <param name="retry">重试计数器</param>
        public void WaitSent(RetryManager retry = null)
        {
            retry ??= new();
            do
            {
                if (!_semaphore.WaitOne(retry.Timeout))
                    retry.Set();
                if (_status == MessageStatus.Failed)
                    retry.Trigger();
            } while (_status != MessageStatus.Sent);
        }

        /// <summary>
        /// 等待回复
        /// </summary>
        /// <param name="retry">重试计数器</param>
        public void WaitReplied(RetryManager retry = null)
        {
            retry ??= new();
            do
            {
                if (!_semaphore.WaitOne(retry.Timeout))
                    retry.Set();
                if (_status == MessageStatus.Failed)
                    retry.Trigger();
            } while (_status != MessageStatus.Replied && retry.Set());
        }
    }
}
