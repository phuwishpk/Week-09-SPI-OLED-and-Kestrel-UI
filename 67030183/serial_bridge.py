#!/usr/bin/env python3
import serial
import time
import urllib.request
import sys

PORT = "/dev/cu.usbserial-0001"
BAUD = 115200
API_URL = "http://localhost:5117/api/telemetry/push?raw="

print(f"Connecting to ESP32 on {PORT} at {BAUD} baud...")
try:
    ser = serial.Serial(PORT, BAUD, timeout=1)
    time.sleep(1)
    print("Serial connected! Streaming live ADC from ESP32 to Kestrel Web Server...")
except Exception as e:
    print(f"Error opening serial port {PORT}: {e}")
    sys.exit(1)

last_push = 0
while True:
    try:
        line = ser.readline().decode('utf-8', errors='ignore').strip()
        if line.startswith("ADC:"):
            raw_str = line.split(":", 1)[1]
            raw_val = int(raw_str)
            now = time.time()
            if now - last_push >= 0.04: # Up to 25 Hz
                last_push = now
                try:
                    req = urllib.request.Request(API_URL + str(raw_val))
                    with urllib.request.urlopen(req, timeout=0.1) as resp:
                        pass
                except Exception:
                    pass
    except KeyboardInterrupt:
        break
    except Exception as e:
        time.sleep(0.05)

ser.close()
