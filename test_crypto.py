#!/usr/bin/env python3
import base64

# Test 1: Check IV byte length with UTF-8
iv_string = "@@@@&&&&####$$$$"
utf8_bytes = iv_string.encode('utf-8')
ascii_bytes = iv_string.encode('ascii')

print(f"IV string: '{iv_string}'")
print(f"UTF-8 byte count: {len(utf8_bytes)}")
print(f"UTF-8 bytes: {utf8_bytes.hex()}")
print(f"ASCII byte count: {len(ascii_bytes)}")
print(f"ASCII bytes: {ascii_bytes.hex()}")
print()

# Test 2: Check Base64 encoding of 3 bytes (never has padding)
test_bytes = bytes([0x01, 0x02, 0x03])
base64_result = base64.b64encode(test_bytes).decode('ascii')
print(f"3 bytes as Base64: '{base64_result}' (length: {len(base64_result)})")
print(f"Contains '=' padding: {'=' in base64_result}")
print()

# Test 3: Multiple 3-byte samples
import random
for i in range(5):
    random_bytes = bytes([random.randint(0, 255) for _ in range(3)])
    b64 = base64.b64encode(random_bytes).decode('ascii')
    print(f"Sample {i+1}: {random_bytes.hex()} -> '{b64}' (len={len(b64)}, has '=': {'=' in b64})")
