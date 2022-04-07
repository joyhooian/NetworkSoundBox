from socket import *
import time, requests
from typing import ByteString
import threading
import hmac
import queue

ERROR = 'error'

isDownloading = False

inbox = queue.Queue(maxsize=0)
outbox = queue.Queue(maxsize=0)
fileQueue = queue.Queue(maxsize=0)
client = socket(AF_INET, SOCK_STREAM)

def Main():
    # 基本设置
    # region 参数设置
    apiKey = 'abcdefg'
    secretKey = 'hengliyuan123'
    sn = ''
    host = ''
    port = 10808
    deviceCode = 0x00
    # endregion
    
    # target = 'server'
    target = 'local'
    # deviceType = '4G'
    deviceType = 'WiFi'
    # sn = '0065a0fa'

    # region 环境变量设置
    if target == 'local':
        host = '127.0.0.1'
    else:
        host = '8.130.51.198'
    
    if deviceType == '4G':
        sn = '02387448'
        deviceCode = 0x11
    else:
        sn = '00eff4ea'
        deviceCode = 0x01
    #endregion
    
    # 连接服务器
    client.connect((host, port))
    poll = []
    poll.append(threading.Thread(target=HandleSocket, args=(sn,secretKey, deviceCode)))
    poll.append(threading.Thread(target=HandleOutbox, args=(isDownloading,)))
    poll.append(threading.Thread(target=HandleInbox, args=(isDownloading,sn, apiKey, host)))
    poll.append(threading.Thread(target=HandleFile, args=(isDownloading,))) 
    poll.append(threading.Thread(target=HandleHeartbeat, args=(isDownloading,)))
    for thread in poll:
        thread.start()

# 登陆并接受消息
def HandleSocket(sn: str, secretKey: str, deviceCode: int):
    # 计算登陆Token
    auth = GetAuthorization(sn, secretKey, deviceCode)
    # 发送登陆命令
    client.send(PackSendBuffer(0x01, auth), 0)
    while True:
        #接受消息并解析
        recvData = client.recv(300)
        res = ParseMessage(recvData)
        if res == ERROR: 
            return ERROR

        inbox.put(res)

# 分发接收到的消息
def HandleInbox(isDownloading: bool, snStr: str, apiKeyStr: str, host: str):
    while True:
        message = inbox.get()
        # region 收到登陆回复
        if message['cmd'] == 0x01:
            if (message['data'].decode('ascii') != Authorize(snStr, apiKeyStr)):
                print('服务器校验失败')
                client.close()
            print('登陆成功')

        # endregion
        # region 收到下载文件命令
        if message['cmd'] == 0xA0:
            fileQueue.put({
                'cmd': message['cmd'], 
                'fileIdx': int(message['data'][0]),
                'pkgCnt': int.from_bytes(message['data'][1:3], 'big', signed=False)
            })
        # endregion
        # region 收到文件分包
        if message['cmd'] == 0xA1:
            fileQueue.put({
                'cmd': message['cmd'],
                'pkgIdx': int.from_bytes(message['data'][0:2], 'big', signed=False),
                'data': message['data'][2:257],
                'crc': message['data'][257]
            })
        # endregion
        # region 收到文件结束命令
        if message['cmd'] == 0xA3:
            fileQueue.put({
                'cmd': message['cmd'],
                'fileIdx': int(message['data'][1])
            })
        # endregion
        # region 收到4G文件下载通知
        if message['cmd'] == 0xA4:
            print(str(message['data'], 'ascii'))
            outbox.put({
                'cmd': message['cmd'],
                'data': bytearray()
            })
            requests.get('http://' + host + ':5000/api/device_ctrl/download_file_stream?fileToken=' + str(message['data'], 'ascii'))
        # endregion
        # region 收到定时命令
        if message['cmd'] == 0x23 or message['cmd'] == 0x24:
            outbox.put({
                'cmd': message['cmd'],
                'data': bytearray()
            })
            print(message['data'])
        # endregion
        # region 收到音频控制命令
        if 0xF0 <= message['cmd'] and message['cmd'] <= 0xF9:
            if message['cmd'] == 0xF7:
                index = message['data'][0] << 8 | message['data'][1]
                if 1 <= index and index <= 6:
                    outbox.put({
                        'cmd': message['cmd'],
                        'data': message['data']
                    })
                else:
                    outbox.put({
                        'cmd': message['cmd'],
                        'data': bytearray(0x00)
                    })
            elif message['cmd'] == 0xF8:
                outbox.put({
                    'cmd': message['cmd'],
                    'data': bytearray([0x00, 0x06])
                })
            elif message['cmd'] == 0xF9:
                index = message['data'][0] << 8 | message['data'][1]
                if 1 <= index and index <= 6:
                    outbox.put({
                        'cmd': message['cmd'],
                        'data': message['data']
                    })
                else:
                    outbox.put({
                        'cmd': message['cmd'],
                        'data': bytearray(0x00)
                    })
            else:
                outbox.put({
                    'cmd': message['cmd'],
                    'data': bytearray()
            })
        # endregion
        # region 收到设备控制命令
        if 0x10 <= message['cmd'] and message['cmd'] <= 0x11:
            outbox.put({
                'cmd': message['cmd'],
                'data': bytearray()
            })
        # endregion

# 发送消息
def HandleOutbox(isDownloading: bool):
    while True:
        # 等待Outbox数据
        message = outbox.get()
            # 发送消息
        client.send(PackSendBuffer(message['cmd'], message['data']), 0)
        # if not isDownloading:
            # print("发送成功 命令: 0x%02x, 长度: %d"%(message['cmd'], len(message['data'])))

# 每20s发送一次心跳信号
def HandleHeartbeat(isDownloading: bool):
    while True:
        if not isDownloading:
            # print("发送心跳信号")
            outbox.put({
                'cmd': 0x02,
                'data': bytearray([0x00, 0x00])
            })
        time.sleep(20)

# 文件下载
def HandleFile(isDownloading: bool):
    # 文件编号
    fileIndex = 0
    # 文件分包总数
    packageCount = 0
    # 文件分包序号
    packageIndex = 0
    # 文件缓存
    fileBuffer = bytearray()
    # 下载起始时间
    startTime = time.time()
    # 下载结束时间
    endTime = time.time()

    while True:
        # 从队列获取文件信息
        message = fileQueue.get()
        # 有文件需要下载
        if message['cmd'] == 0xA0:
            fileIndex = message['fileIdx']
            packageCount = message['pkgCnt']
            packageIndex += 1
            isDownloading = True
            startTime = time.time()
            print("进入下载模式，文件序号%d，总包数%d(%.1fKB)"%(fileIndex, packageCount, packageCount * 255.0 / 1024.0))
            outbox.put({
                'cmd': 0xA0,
                'data': bytearray([0x00, 0x00])
            })
        # 下载文件分包
        if message['cmd'] == 0xA1:
            # 检查包序号
            if message['pkgIdx'] != packageIndex: 
                print('包序号错误')
            # 校验文件完整性
            if not FileVirifyCRC(message['data'], message['crc']): 
                print('文件校验错误')
            # 校验成功，存入缓存
            fileBuffer += message['data']
            print("\r%02.1f%%"%(packageIndex/packageCount*100), end="")
            # 回复
            outbox.put({
                'cmd': 0xA0,
                'data': packageIndex.to_bytes(2, 'big', signed=False)
            })
            packageIndex += 1
        # 文件下载完毕
        if message['cmd'] == 0xA3:
            if message['fileIdx'] != 0 and fileIndex != message['fileIdx']: 
                print('文件编号错误')
            if message['fileIdx'] == fileIndex:
                endTime = time.time()
                print("\n文件接收完毕, 耗时：%.1fs, 速度：%.1fKB/s"%(endTime-startTime, len(fileBuffer)/1024.0/(endTime-startTime)))
            # 重置变量
            isDownloading = False
            packageCount = 0
            packageIndex = 0
            fileBuffer = bytearray()
            startTime = 0
            endTime = 0
            # 回复
            outbox.put({
                'cmd': 0xA3,
                'data': fileIndex.to_bytes(2, 'big', signed=False)
            })

# 校验登陆返回消息
def Authorize(snStr: str, apiKeyStr: str):
    # 获取时间戳
    timeStamp = int(time.time())
    timeStamp += (0 if timeStamp % 10 < 5 else 10) - timeStamp % 10
    timeStampStr = str(timeStamp)
    # 第一次加密
    keyStr = hmac.new(apiKeyStr.encode('ascii'), snStr.encode('ascii'), digestmod='MD5').hexdigest()
    # 第二次加密
    keyStr = hmac.new(keyStr.encode('ascii'), timeStampStr.encode('ascii'), digestmod='MD5').hexdigest()
    return keyStr

# 组装发送Buffer
def PackSendBuffer(cmd, data: bytearray):
    # 预处理
    if isinstance(cmd, int): cmd = cmd.to_bytes(1, 'big', signed=False)

    sendBuf = bytearray(b'\x7e')
    sendBuf += cmd
    sendBuf += bytearray(len(data).to_bytes(2, byteorder='big', signed=False))
    sendBuf += data
    sendBuf += b'\xef'

    return sendBuf

# 计算登陆Token
def GetAuthorization(snStr: str, secretStr: str, deviceCode: int):
    # 第一次加密
    keyStr = hmac.new(secretStr.encode('ascii'), snStr.encode('ascii'), digestmod='MD5').hexdigest()

    # 获取当前时区整十秒时间戳
    timeStamp = int(time.time())
    timeStamp += (0 if timeStamp % 10 < 5 else 10) - timeStamp % 10
    if deviceCode == 0x11:
        timeStampStr = time.strftime("%y/%m/%d, %X", time.localtime(timeStamp))
    else:
        timeStampStr = str(timeStamp)

    # 第二次加密
    keyStr = hmac.new(keyStr.encode('ascii'), timeStampStr.encode('ascii'), digestmod='MD5').hexdigest()

    # 生成LoginBuf
    keyBuf = bytearray()
    keyBuf += bytearray(snStr.encode('ascii'))
    keyBuf += bytearray(keyStr.encode('ascii'))
    keyBuf += bytearray([deviceCode])

    return keyBuf

# 解析消息入队
def ParseMessage(recvData: bytearray):

    # 判断接收到的长度
    if len(recvData) >= 5:

        # 寻找Frame头字节
        startOffset = recvData.index(0x7E)
        if startOffset == -1: 
            return ERROR

        # 获取Data域长度
        dataLen = GetDataLen(recvData, startOffset)
        if dataLen == ERROR: 
            return ERROR

        # 获取Data域
        if dataLen == 258:
            data = recvData[2:(dataLen+2)]
        else:
            data = recvData[4:(dataLen+4)]
        
        return { 'cmd': recvData[startOffset + 1], 'data': data }

    return ERROR

# 检查文件完整性
def FileVirifyCRC(data: bytearray, crc: bytes):
    crcSum = 0
    for byte in data:
        crcSum += byte
    crcSum &= 0x00FF
    return crcSum == crc

# 消息预处理
def GetDataLen(recvData: bytearray, startOffset: int):

    #越界检查
    if startOffset + 3 > len(recvData): 
        return ERROR

    if recvData[startOffset + 1] == 0xA1:
        return 258

    #数据域长度
    dataLen = int(recvData[startOffset + 2]) | int(recvData[startOffset + 3])

    #越界检查
    endOffset = dataLen + 4 if dataLen + 4 < len(recvData) else -1
    if endOffset == -1: 
        return ERROR

    #检查数据结尾和Command
    index = CMD_LIST.index(recvData[startOffset + 1])
    if recvData[endOffset] == 0xEF and index != -1:
        return dataLen

    return ERROR


CMD_LIST = bytearray([0x01, 0x02, 0x10, 0x11, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9])

Main()