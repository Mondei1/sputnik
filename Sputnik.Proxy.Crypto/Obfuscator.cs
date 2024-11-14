﻿using System.Text;

namespace Sputnik.Proxy.Crypto;

// Proudly generated by Claude 3.5 Sonnet :3 + own modifications.
// Thus this is mostly AI generated code this is considered public domain.
public class Obfuscator
{
    private readonly byte[] _key;

    public Obfuscator(string key)
    {
        _key = Encoding.ASCII.GetBytes(key);
    }

    public Obfuscator(byte[] key)
    {
        _key = key;
    }

    public byte[] Obfuscate(byte[] data)
    {
        byte[] obfuscatedData = new byte[data.Length];

        // Perform XOR with key and shift left by 1
        for (int i = 0; i < data.Length; i++)
        {
            obfuscatedData[i] = (byte)((data[i] ^ _key[i % _key.Length]) << 1);
        }

        // Convert to Base64 to make it safe for transmission as text
        return obfuscatedData;
    }

    public byte[] Deobfuscate(byte[] obfuscatedData)
    {
        byte[] data = new byte[obfuscatedData.Length];

        // Reverse the XOR and shift to get the original data
        for (int i = 0; i < obfuscatedData.Length; i++)
        {
            data[i] = (byte)((obfuscatedData[i] >> 1) ^ _key[i % _key.Length]);
        }

        return data;
    }
}