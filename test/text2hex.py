from tkinter import *
from tkinter import ttk
from tkinter.filedialog import (askopenfile, askopenfilename, 
                                    askopenfilenames, 
                                    askdirectory, 
                                    asksaveasfilename)
import struct                                    


root = Tk()
root.title("hello")
root.minsize(300,200)

filePath = StringVar()

def OpenFile():
  fp = askopenfile()
  filePath.set(fp.name)

def ConvertFile():
  fp = filePath.get()
  f = open(fp, 'r')
  text = f.read()
  hext = text.replace(' ', '')
  numList = []
  for offset in range(0, len(hext), 2):
    x = hext[offset:offset+2]
    h = int(x, 16)
    numList.append(h)
  with open('./hex.hex', 'wb') as fb:
    for x in numList:
      a = struct.pack('B', x)
      fb.write(a)
    fb.close()
    root.destroy()

frm = ttk.Frame(root, padding=10)
frm.grid()
ttk.Label(frm, text="待转换文件").grid(column=0, row=0)
ttk.Button(frm, text="选择文件", command=OpenFile).grid(column=0, row=1)
ttk.Button(frm, text="转换", command=ConvertFile).grid(column=0, row=2)
root.mainloop()