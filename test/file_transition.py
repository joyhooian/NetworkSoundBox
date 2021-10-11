from socket import *
import time, requests

def Main():

    client = socket(AF_INET, SOCK_STREAM)

    client.connect(("127.0.0.1", 10809))

    client.send(bytearray([0x7e, 0x01, 0x00, 0x04, 0x30, 0x30, 0x30, 0x31, 0xef]), 0)

    recvData = client.recv(300)

    PrintData(recvData)

    res = ParseMessage(recvData)

    if res == -1: return

    if res[0] == 0x01: print("Logged in")

    while True:

        time.sleep(1)

        url = "http://127.0.0.1:5000/Soundbox/TTS/SN0001Text%E5%A5%BD%E4%BD%A0%E5%A5%BD%E4%BD%A0%E5%A5%BD%E4%BD%A0%E5%A5%BD%E4%BD%A0%E5%A5%BD%E4%BD%A0%E5%A5%BD%E4%BD%A0%E5%A5%BD%E4%BD%A0%E5%A5%BD%E4%BD%A0%E5%A5%BD%E4%BD%A0%E5%A5%BD"

        requests.get(url)

        recvData = client.recv(300)
        PrintData(recvData)

        res = ParseMessage(recvData)
        if res == -1: continue

        if res[0] == 0xA0 and len(res[1]) == 3:
            fileIndex = res[1][0]
            pkgCount = res[1][1] | res[1][2]
            print("进入下载模式，文件序号%d，总包数%d(%.1fKB)"%(fileIndex, pkgCount, pkgCount * 255.0 / 1024.0))
            startTime = time.time()
            client.send(bytearray([0x7e, 0xA0, 0x00, 0x02, 0x00, 0x00, 0xef]))
            res = DownloadFile(client, pkgCount)
            if res == -1: return -1
            endTime = time.time()
            print("文件接收完毕, 耗时：%.1fs, 速度：%.1fKB/s"%(endTime-startTime, len(res)/1024.0/(endTime-startTime)))

            recvData = client.recv(300)
            res = ParseMessage(recvData)
            if res == -1: return -1
            if res[0] == 0xA3:
                client.send(recvData)

            recvData = client.recv(300)
            res = ParseMessage(recvData)
            if res == -1: return -1
            if res[0] == 0xA3 and len(res[1]) == 2 and res[1][0] == 0x00 and res[1][1] == 0x00:
                client.send(recvData)

def DownloadFile(client:socket, pkgCount):
    pkgIndex = 0
    recvFile = bytearray()
    while pkgIndex < pkgCount:
        pkgIndex += 1
        recvData = client.recv(300)
        print("\r%02.1f%%"%(pkgIndex/pkgCount*100), end="")
        # PrintData(recvData)

        res = RecvFile(recvData, pkgIndex)
        if res == -1: return -1
        for byte in res:
            recvFile.append(byte)
        client.send(bytearray([0x7E, 0xA0, 0x00, 0x02, pkgIndex & 0xFF00, pkgIndex & 0x00FF, 0xEF]))
    print("")
    return recvFile

def PrintData(recvData):
    for i in recvData:
        print("0x%02x"%i, end=" ")
    print()

def ParseMessage(recvData: bytearray):
    if len(recvData) >= 5:
        startOffset = recvData.index(0x7E)
        if startOffset == -1: return -1

        dataLen = CheckMessage(recvData, startOffset)
        if dataLen == -1: return -1

        data = bytearray()
        for i in range(dataLen):
            data.append(recvData[i + 4])
        
        return [recvData[startOffset + 1], data]
    return -1

def RecvFile(recvData: bytearray, pkgIndex: int):
    if len(recvData) < 5: return -1

    startOffset = recvData.index(0x7E)
    if startOffset == -1: return -1

    if recvData[startOffset + 1] != 0xA1: return -1

    if recvData[startOffset + 4 + 256] != 0xEF: return -1 

    if pkgIndex != recvData[startOffset + 2] | recvData[startOffset + 3]: return -1

    pkgData = bytearray()
    for i in range(255):
        pkgData.append(recvData[i + 4])
    
    crc = 0
    for byte in pkgData:
        crc += byte
    crc &= 0x00FF
    if crc != recvData[255 + 4]: return -1

    return pkgData

def CheckMessage(recvData: bytearray, startOffset: int):

    #越界检查
    if startOffset + 3 > len(recvData): return -1

    #数据域长度
    dataLen = int(recvData[startOffset + 2]) | int(recvData[startOffset + 3])

    #越界检查
    endOffset = dataLen + 4 if dataLen + 4 < len(recvData) else -1
    if endOffset == -1: return -1

    #检查数据结尾和Command
    index = CMD_LIST.index(recvData[startOffset + 1])
    if int(recvData[endOffset]) == 0xEF and index != -1:
        return dataLen

    return -1

CMD_LIST = bytearray([0x01, 0x02, 0xA0, 0xA1, 0xA2, 0xA3])

Main()