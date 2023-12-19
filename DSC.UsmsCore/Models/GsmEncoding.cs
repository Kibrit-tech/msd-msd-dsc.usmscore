using System;
using System.Collections.Generic;

namespace DSC.UsmsCore.Models
{
    /// <summary>
    ///     Text encoding class for the GSM 03.38 alphabet.
    ///     Converts between GSM and the internal .NET Unicode character representation
    /// </summary>
    public class GsmEncoding : System.Text.Encoding
    {
        private SortedDictionary<char, byte[]> _charToByteDictionary;
        private SortedDictionary<uint, char> _byteToCharDictionary;

        public GsmEncoding()
        {
            PopulateDictionaries();
        }

        public override int GetByteCount(char[] chars, int index, int count)
        {
            var byteCount = 0;
            if(chars == null) throw new ArgumentNullException(nameof(chars));
            if(index < 0 || index > chars.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if(count < 0 || count > chars.Length - index) throw new ArgumentOutOfRangeException(nameof(count));

            for (var i = index; i < count; i++)
                if (_charToByteDictionary.ContainsKey(chars[i]))
                    byteCount += _charToByteDictionary[chars[i]].Length;

            return byteCount;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            var byteCount = 0;

            // Validate the parameters.
            if(chars == null) throw new ArgumentNullException(nameof(chars));

            if(bytes == null) throw new ArgumentNullException(nameof(bytes));

            if(charIndex < 0 || charIndex > chars.Length)
                throw new ArgumentOutOfRangeException(nameof(charIndex));

            if(charCount < 0 || charCount > chars.Length - charIndex)
                throw new ArgumentOutOfRangeException(nameof(charCount));

            if(byteIndex < 0 || byteIndex > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(byteIndex));

            if(byteIndex + GetByteCount(chars, charIndex, charCount) > bytes.Length)
                throw new ArgumentException("bytes array too small", nameof(bytes));

            for (var i = charIndex; i < charIndex + charCount; i++)
            {
                byte[] charByte;
                if(_charToByteDictionary.TryGetValue(chars[i], out charByte))
                {
                    charByte.CopyTo(bytes, byteIndex + byteCount);
                    byteCount += charByte.Length;
                }
            }

            return byteCount;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            var charCount = 0;
            if(bytes == null) throw new ArgumentNullException(nameof(bytes));

            if(index < 0 || index > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            if(count < 0 || count > bytes.Length - index)
                throw new ArgumentOutOfRangeException(nameof(count));

            var i = index;
            while (i < index + count)
            {
                if(bytes[i] <= 0x7F)
                    if(bytes[i] == 0x1B)
                    {
                        i++;
                        if(i < bytes.Length && bytes[i] <= 0x7F) charCount++; // GSM Spec says replace 1B 1B with space
                    }
                    else
                    {
                        charCount++;
                    }
                i++;
            }

            return charCount;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            var charCount = 0;

            // Validate the parameters.
            if(bytes == null) throw new ArgumentNullException(nameof(bytes));

            if(chars == null) throw new ArgumentNullException(nameof(chars));

            if(byteIndex < 0 || byteIndex > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(byteIndex));

            if(byteCount < 0 || byteCount > bytes.Length - byteIndex)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            if(charIndex < 0 || charIndex > chars.Length)
                throw new ArgumentOutOfRangeException(nameof(charIndex));

            if(charIndex + GetCharCount(bytes, byteIndex, byteCount) > chars.Length)
                throw new ArgumentException("chars array too small", nameof(chars));

            var i = byteIndex;
            while (i < byteIndex + byteCount)
            {
                if(bytes[i] <= 0x7F)
                    if(bytes[i] == 0x1B)
                    {
                        i++;
                        if(i < bytes.Length && bytes[i] <= 0x7F)
                        {
                            char nextChar;
                            var extendedChar = 0x1B * 255 + (uint) bytes[i];
                            if(_byteToCharDictionary.TryGetValue(extendedChar, out nextChar))
                            {
                                chars[charCount] = nextChar;
                                charCount++;
                            }

                            // GSM Spec says to try for normal character if escaped one doesn't exist
                            else if(_byteToCharDictionary.TryGetValue(bytes[i], out nextChar))
                            {
                                chars[charCount] = nextChar;
                                charCount++;
                            }
                        }
                    }
                    else
                    {
                        chars[charCount] = _byteToCharDictionary[bytes[i]];
                        charCount++;
                    }
                i++;
            }

            return charCount;
        }

        public override int GetMaxByteCount(int charCount)
        {
            if(charCount < 0)
                throw new ArgumentOutOfRangeException(nameof(charCount));

            return charCount * 2;
        }

        public override int GetMaxCharCount(int byteCount)
        {
            if(byteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            return byteCount;
        }

        private void PopulateDictionaries()
        {
            // Unicode char to GSM bytes
            _charToByteDictionary = new SortedDictionary<char, byte[]>();

            // GSM bytes to Unicode char
            _byteToCharDictionary = new SortedDictionary<uint, char>();

            _charToByteDictionary.Add('\u0040', new byte[] {0x00});
            _charToByteDictionary.Add('\u00A3', new byte[] {0x01});
            _charToByteDictionary.Add('\u0024', new byte[] {0x02});
            _charToByteDictionary.Add('\u00A5', new byte[] {0x03});
            _charToByteDictionary.Add('\u00E8', new byte[] {0x04});
            _charToByteDictionary.Add('\u00E9', new byte[] {0x05});
            _charToByteDictionary.Add('\u00F9', new byte[] {0x06});
            _charToByteDictionary.Add('\u00EC', new byte[] {0x07});
            _charToByteDictionary.Add('\u00F2', new byte[] {0x08});
            _charToByteDictionary.Add('\u00C7', new byte[] {0x09});
            _charToByteDictionary.Add('\u000A', new byte[] {0x0A});
            _charToByteDictionary.Add('\u00D8', new byte[] {0x0B});
            _charToByteDictionary.Add('\u00F8', new byte[] {0x0C});
            _charToByteDictionary.Add('\u000D', new byte[] {0x0D});
            _charToByteDictionary.Add('\u00C5', new byte[] {0x0E});
            _charToByteDictionary.Add('\u00E5', new byte[] {0x0F});
            _charToByteDictionary.Add('\u0394', new byte[] {0x10});
            _charToByteDictionary.Add('\u005F', new byte[] {0x11});
            _charToByteDictionary.Add('\u03A6', new byte[] {0x12});
            _charToByteDictionary.Add('\u0393', new byte[] {0x13});
            _charToByteDictionary.Add('\u039B', new byte[] {0x14});
            _charToByteDictionary.Add('\u03A9', new byte[] {0x15});
            _charToByteDictionary.Add('\u03A0', new byte[] {0x16});
            _charToByteDictionary.Add('\u03A8', new byte[] {0x17});
            _charToByteDictionary.Add('\u03A3', new byte[] {0x18});
            _charToByteDictionary.Add('\u0398', new byte[] {0x19});
            _charToByteDictionary.Add('\u039E', new byte[] {0x1A});

            //_charToByte.Add('\u001B', new byte[] { 0x1B }); // Should we convert Unicode escape to GSM?
            _charToByteDictionary.Add('\u00C6', new byte[] {0x1C});
            _charToByteDictionary.Add('\u00E6', new byte[] {0x1D});
            _charToByteDictionary.Add('\u00DF', new byte[] {0x1E});
            _charToByteDictionary.Add('\u00C9', new byte[] {0x1F});
            _charToByteDictionary.Add('\u0020', new byte[] {0x20});
            _charToByteDictionary.Add('\u0021', new byte[] {0x21});
            _charToByteDictionary.Add('\u0022', new byte[] {0x22});
            _charToByteDictionary.Add('\u0023', new byte[] {0x23});
            _charToByteDictionary.Add('\u00A4', new byte[] {0x24});
            _charToByteDictionary.Add('\u0025', new byte[] {0x25});
            _charToByteDictionary.Add('\u0026', new byte[] {0x26});
            _charToByteDictionary.Add('\u0027', new byte[] {0x27});
            _charToByteDictionary.Add('\u0028', new byte[] {0x28});
            _charToByteDictionary.Add('\u0029', new byte[] {0x29});
            _charToByteDictionary.Add('\u002A', new byte[] {0x2A});
            _charToByteDictionary.Add('\u002B', new byte[] {0x2B});
            _charToByteDictionary.Add('\u002C', new byte[] {0x2C});
            _charToByteDictionary.Add('\u002D', new byte[] {0x2D});
            _charToByteDictionary.Add('\u002E', new byte[] {0x2E});
            _charToByteDictionary.Add('\u002F', new byte[] {0x2F});
            _charToByteDictionary.Add('\u0030', new byte[] {0x30});
            _charToByteDictionary.Add('\u0031', new byte[] {0x31});
            _charToByteDictionary.Add('\u0032', new byte[] {0x32});
            _charToByteDictionary.Add('\u0033', new byte[] {0x33});
            _charToByteDictionary.Add('\u0034', new byte[] {0x34});
            _charToByteDictionary.Add('\u0035', new byte[] {0x35});
            _charToByteDictionary.Add('\u0036', new byte[] {0x36});
            _charToByteDictionary.Add('\u0037', new byte[] {0x37});
            _charToByteDictionary.Add('\u0038', new byte[] {0x38});
            _charToByteDictionary.Add('\u0039', new byte[] {0x39});
            _charToByteDictionary.Add('\u003A', new byte[] {0x3A});
            _charToByteDictionary.Add('\u003B', new byte[] {0x3B});
            _charToByteDictionary.Add('\u003C', new byte[] {0x3C});
            _charToByteDictionary.Add('\u003D', new byte[] {0x3D});
            _charToByteDictionary.Add('\u003E', new byte[] {0x3E});
            _charToByteDictionary.Add('\u003F', new byte[] {0x3F});
            _charToByteDictionary.Add('\u00A1', new byte[] {0x40});
            _charToByteDictionary.Add('\u0041', new byte[] {0x41});
            _charToByteDictionary.Add('\u0042', new byte[] {0x42});
            _charToByteDictionary.Add('\u0043', new byte[] {0x43});
            _charToByteDictionary.Add('\u0044', new byte[] {0x44});
            _charToByteDictionary.Add('\u0045', new byte[] {0x45});
            _charToByteDictionary.Add('\u0046', new byte[] {0x46});
            _charToByteDictionary.Add('\u0047', new byte[] {0x47});
            _charToByteDictionary.Add('\u0048', new byte[] {0x48});
            _charToByteDictionary.Add('\u0049', new byte[] {0x49});
            _charToByteDictionary.Add('\u004A', new byte[] {0x4A});
            _charToByteDictionary.Add('\u004B', new byte[] {0x4B});
            _charToByteDictionary.Add('\u004C', new byte[] {0x4C});
            _charToByteDictionary.Add('\u004D', new byte[] {0x4D});
            _charToByteDictionary.Add('\u004E', new byte[] {0x4E});
            _charToByteDictionary.Add('\u004F', new byte[] {0x4F});
            _charToByteDictionary.Add('\u0050', new byte[] {0x50});
            _charToByteDictionary.Add('\u0051', new byte[] {0x51});
            _charToByteDictionary.Add('\u0052', new byte[] {0x52});
            _charToByteDictionary.Add('\u0053', new byte[] {0x53});
            _charToByteDictionary.Add('\u0054', new byte[] {0x54});
            _charToByteDictionary.Add('\u0055', new byte[] {0x55});
            _charToByteDictionary.Add('\u0056', new byte[] {0x56});
            _charToByteDictionary.Add('\u0057', new byte[] {0x57});
            _charToByteDictionary.Add('\u0058', new byte[] {0x58});
            _charToByteDictionary.Add('\u0059', new byte[] {0x59});
            _charToByteDictionary.Add('\u005A', new byte[] {0x5A});
            _charToByteDictionary.Add('\u00C4', new byte[] {0x5B});
            _charToByteDictionary.Add('\u00D6', new byte[] {0x5C});
            _charToByteDictionary.Add('\u00D1', new byte[] {0x5D});
            _charToByteDictionary.Add('\u00DC', new byte[] {0x5E});
            _charToByteDictionary.Add('\u00A7', new byte[] {0x5F});
            _charToByteDictionary.Add('\u00BF', new byte[] {0x60});
            _charToByteDictionary.Add('\u0061', new byte[] {0x61});
            _charToByteDictionary.Add('\u0062', new byte[] {0x62});
            _charToByteDictionary.Add('\u0063', new byte[] {0x63});
            _charToByteDictionary.Add('\u0064', new byte[] {0x64});
            _charToByteDictionary.Add('\u0065', new byte[] {0x65});
            _charToByteDictionary.Add('\u0066', new byte[] {0x66});
            _charToByteDictionary.Add('\u0067', new byte[] {0x67});
            _charToByteDictionary.Add('\u0068', new byte[] {0x68});
            _charToByteDictionary.Add('\u0069', new byte[] {0x69});
            _charToByteDictionary.Add('\u006A', new byte[] {0x6A});
            _charToByteDictionary.Add('\u006B', new byte[] {0x6B});
            _charToByteDictionary.Add('\u006C', new byte[] {0x6C});
            _charToByteDictionary.Add('\u006D', new byte[] {0x6D});
            _charToByteDictionary.Add('\u006E', new byte[] {0x6E});
            _charToByteDictionary.Add('\u006F', new byte[] {0x6F});
            _charToByteDictionary.Add('\u0070', new byte[] {0x70});
            _charToByteDictionary.Add('\u0071', new byte[] {0x71});
            _charToByteDictionary.Add('\u0072', new byte[] {0x72});
            _charToByteDictionary.Add('\u0073', new byte[] {0x73});
            _charToByteDictionary.Add('\u0074', new byte[] {0x74});
            _charToByteDictionary.Add('\u0075', new byte[] {0x75});
            _charToByteDictionary.Add('\u0076', new byte[] {0x76});
            _charToByteDictionary.Add('\u0077', new byte[] {0x77});
            _charToByteDictionary.Add('\u0078', new byte[] {0x78});
            _charToByteDictionary.Add('\u0079', new byte[] {0x79});
            _charToByteDictionary.Add('\u007A', new byte[] {0x7A});
            _charToByteDictionary.Add('\u00E4', new byte[] {0x7B});
            _charToByteDictionary.Add('\u00F6', new byte[] {0x7C});
            _charToByteDictionary.Add('\u00F1', new byte[] {0x7D});
            _charToByteDictionary.Add('\u00FC', new byte[] {0x7E});
            _charToByteDictionary.Add('\u00E0', new byte[] {0x7F});

            // Extended GSM
            _charToByteDictionary.Add('\u20AC', new byte[] {0x1B, 0x65});
            _charToByteDictionary.Add('\u000C', new byte[] {0x1B, 0x0A});
            _charToByteDictionary.Add('\u005B', new byte[] {0x1B, 0x3C});
            _charToByteDictionary.Add('\u005C', new byte[] {0x1B, 0x2F});
            _charToByteDictionary.Add('\u005D', new byte[] {0x1B, 0x3E});
            _charToByteDictionary.Add('\u005E', new byte[] {0x1B, 0x14});
            _charToByteDictionary.Add('\u007B', new byte[] {0x1B, 0x28});
            _charToByteDictionary.Add('\u007C', new byte[] {0x1B, 0x40});
            _charToByteDictionary.Add('\u007D', new byte[] {0x1B, 0x29});
            _charToByteDictionary.Add('\u007E', new byte[] {0x1B, 0x3D});

            foreach (var charToByte in _charToByteDictionary)
            {
                uint charByteValue = 0;
                if(charToByte.Value.Length == 1)
                    charByteValue = charToByte.Value[0];
                else if(charToByte.Value.Length == 2)
                    charByteValue = (uint) charToByte.Value[0] * 255 + charToByte.Value[1];

                _byteToCharDictionary.Add(charByteValue, charToByte.Key);
            }

            _byteToCharDictionary.Add(0x1B1B, '\u0020'); // GSM char set says to map 1B1B to a space
        }
    }
}