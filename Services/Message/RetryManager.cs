using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Message
{
    public class RetryManager
    {
        private const int MAX_RETRY_TIMES = 3;
        private const int DEFAULT_TIMEOUT_LONG = 5 * 1000;
        private readonly int _maxRetryTimes;
        private readonly OverflowCallBack _overflowCallBack;
        private readonly object[] _callbackArgs;

        public delegate void OverflowCallBack(params object[] args);
        public bool IsOverflow => Count > _maxRetryTimes;
        public int Timeout { get; }
        public int Count { get; private set; } = 0;

        /// <summary>
        /// 重试计数器构造函数
        /// </summary>
        /// <param name="maxRetryTimes">最大重试次数</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="callBack">超时回调函数</param>
        /// <param name="args">回调函数参数</param>
        public RetryManager(int maxRetryTimes = MAX_RETRY_TIMES,int timeout = DEFAULT_TIMEOUT_LONG, OverflowCallBack callBack = null, params object[] args)
        {
            _maxRetryTimes = maxRetryTimes;
            Timeout = timeout;
            _overflowCallBack = callBack ??= new OverflowCallBack((o) => throw new TimeoutException());
            _callbackArgs = args;
        }

        /// <summary>
        /// 增加重试计数
        /// </summary>
        /// <returns> true: 重试未溢出 false: 重试溢出</returns>
        public bool Set()
        {
            if (++Count > _maxRetryTimes)
            {
                _overflowCallBack(_callbackArgs);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 重试计数清零
        /// </summary>
        public void Reset()
        {
            Count = 0;
        }

        /// <summary>
        /// 触发回调函数
        /// </summary>
        public void Trigger()
        {
            Count = _maxRetryTimes;
        }
    }
}
