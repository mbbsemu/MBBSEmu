using System;
using System.IO;
using System.Text;

namespace MBBSEmu.HostProcess.Structs
{
    public class UserAccount
    {
        /// <summary>
        ///     user-id size (including trailing zero)
        /// </summary>
        public const int UIDSIZ = 30;

        /// <summary>
        ///     password size (ditto)
        /// </summary>
        public const int PSWSIZ = 10;

        /// <summary>
        ///     name/address line size (ditto)
        /// </summary>
        public const int NADSIZ = 30;

        /// <summary>
        ///     phone number field size (ditto) 
        /// </summary>
        public const int PHOSIZ = 16;

        /// <summary>
        ///     key (and class) name size (ditto)
        /// </summary>
        public const int KEYSIZ = 16;

        /// <summary>
        ///     number of ints for "access" info
        /// </summary>
        public const int AXSSIZ = 7;

        /// <summary>
        ///     size of a date in xx/xx/xx format
        /// </summary>
        public const int DATSIZ = 9;

        /// <summary>
        ///     Spare padding bytes
        /// </summary>
        public const int USRACCSPARE = 37;

        /// <summary>
        ///     user-id 
        /// </summary>
        public byte[] userid
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(0, UIDSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 0, UIDSIZ);
        }

        /// <summary>
        ///     password
        /// </summary>
        public byte[] psword
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(30, PSWSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 30, PSWSIZ);
        }

        /// <summary>
        ///     user name
        /// </summary>
        public byte[] usrnam
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(40, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 40, NADSIZ);
        }

        /// <summary>
        ///     address line 1 (company)
        /// </summary>
        public byte[] usrad1
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(70, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 70, NADSIZ);
        }

        /// <summary>
        ///     address line 2  
        /// </summary>
        public byte[] usrad2
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(100, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 100, NADSIZ);
        }

        /// <summary>
        ///     address line 3
        /// </summary>
        public byte[] usrad3
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(130, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 130, NADSIZ);
        }

        /// <summary>
        ///     address line 4
        /// </summary>
        public byte[] usrad4
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(160, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 160, NADSIZ);
        }

        /// <summary>
        ///     phone number
        /// </summary>
        public byte[] usrpho
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(190, PHOSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 190, PHOSIZ);
        }

        /// <summary>
        ///     system type code
        /// </summary>
        public char systyp
        {
            get => (char)_usrAccStructBytes[206];
            set => _usrAccStructBytes[206] = (byte)value;
        }

        /// <summary>
        ///     user preference flags
        /// </summary>
        public char usrprf
        {
            get => (char)_usrAccStructBytes[207];
            set => _usrAccStructBytes[207] = (byte)value;
        }

        /// <summary>
        ///     ANSI flags
        /// </summary>
        public char ansifl
        {
            get => (char)_usrAccStructBytes[208];
            set => _usrAccStructBytes[208] = (byte)value;
        }

        /// <summary>
        ///     screen width in columns
        /// </summary>
        public char scnwid
        {
            get => (char)_usrAccStructBytes[209];
            set => _usrAccStructBytes[209] = (byte)value;
        }

        /// <summary>
        ///     screen length for page breaks
        /// </summary>
        public char scnbrk
        {
            get => (char)_usrAccStructBytes[210];
            set => _usrAccStructBytes[210] = (byte)value;
        }

        /// <summary>
        ///     screen length for FSE stuff
        /// </summary>
        public char scnfse
        {
            get => (char)_usrAccStructBytes[211];
            set => _usrAccStructBytes[211] = (byte)value;
        }

        /// <summary>
        ///     user's age
        /// </summary>
        public char age
        {
            get => (char)_usrAccStructBytes[212];
            set => _usrAccStructBytes[212] = (byte)value;
        }

        /// <summary>
        ///     user's sex ('M' or 'F')
        /// </summary>
        public char sex
        {
            get => (char)_usrAccStructBytes[213];
            set => _usrAccStructBytes[213] = (byte)value;
        }

        /// <summary>
        ///     account creation date
        /// </summary>
        public ushort credat
        {
            get => BitConverter.ToUInt16(_usrAccStructBytes, 214);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 214, 2);
        }

        /// <summary>
        ///     date of last use of account
        /// </summary>
        public ushort usedat
        {
            get => BitConverter.ToUInt16(_usrAccStructBytes, 216);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 216, 2);
        }

        /// <summary>
        ///     classified-ad counts used so far
        /// </summary>
        public short csicnt
        {
            get => BitConverter.ToInt16(_usrAccStructBytes, 218);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 218, 2);
        }

        /// <summary>
        ///     various saved bit flags
        /// </summary>
        public short flags
        {
            get => BitConverter.ToInt16(_usrAccStructBytes, 220);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 220, 2);
        }

        /// <summary>
        ///     array of remote sysop access bits
        /// </summary>
        public byte[] access
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(222, AXSSIZ * 2).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 222, AXSSIZ * 2);
        }

        /// <summary>
        ///     e-mail limit reached so far (new/old bdy)
        /// </summary>
        public int emllim
        {
            get => BitConverter.ToInt32(_usrAccStructBytes, 236);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 236, 4);
        }

        /// <summary>
        ///     class to return user to if necessary
        /// </summary>
        public byte[] prmcls
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(240, KEYSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 240, KEYSIZ);
        }

        /// <summary>
        ///     current class of this user
        /// </summary>
        public byte[] curcls
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(256, KEYSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 256, KEYSIZ);
        }

        /// <summary>
        ///     time user has been online today (in secs)
        /// </summary>
        public int timtdy
        {
            get => BitConverter.ToInt32(_usrAccStructBytes, 272);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 272, 4);
        }

        /// <summary>
        ///     days left in this class (if applicable)
        /// </summary>
        public ushort daystt
        {
            get => BitConverter.ToUInt16(_usrAccStructBytes, 274);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 274, 2);
        }

        /// <summary>
        ///     days since debt was last "forgiven"
        /// </summary>
        public ushort fgvdys
        {
            get => BitConverter.ToUInt16(_usrAccStructBytes, 278);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 278, 2);
        }

        /// <summary>
        ///     credits available or debt (if negative)
        /// </summary>
        public int creds
        {
            get => BitConverter.ToInt32(_usrAccStructBytes, 280);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 280, 4);
        }

        /// <summary>
        ///     total credits ever posted (paid & free)
        /// </summary>
        public int totcreds
        {
            get => BitConverter.ToInt32(_usrAccStructBytes, 284);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 284, 4);
        }

        /// <summary>
        ///     total credits ever posted (paid only)
        /// </summary>
        public int totpaid
        {
            get => BitConverter.ToInt32(_usrAccStructBytes, 288);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _usrAccStructBytes, 288, 4);
        }

        /// <summary>
        ///     this user's birthday date
        /// </summary>
        public byte[] birthd
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = _usrAccStructBytes;
                return userAccSpan.Slice(292, DATSIZ).ToArray();
            }
            set => Array.Copy(value, 0, _usrAccStructBytes, 292, DATSIZ);
        }

        private byte[] _usrAccStructBytes;

        public const ushort Size = 341;

        public UserAccount()
        {
            _usrAccStructBytes = new byte[341];
            flags = 1;
            sex = 'M';  //Set everyone to male for now
            Array.Copy(BitConverter.GetBytes((ushort) 1), 0, access, 0, 2);
        }

        public void FromSpan(ReadOnlySpan<byte> userAccSpan)
        {
            userid = userAccSpan.Slice(0, UIDSIZ).ToArray();
            psword = userAccSpan.Slice(30, PSWSIZ).ToArray();
            usrnam = userAccSpan.Slice(40, NADSIZ).ToArray();
            usrad1 = userAccSpan.Slice(70, NADSIZ).ToArray();
            usrad2 = userAccSpan.Slice(100, NADSIZ).ToArray();
            usrad3 = userAccSpan.Slice(130, NADSIZ).ToArray();
            usrad4 = userAccSpan.Slice(160, NADSIZ).ToArray();
            usrpho = userAccSpan.Slice(190, PHOSIZ).ToArray();
            systyp = (char)userAccSpan[206];
            usrprf = (char)userAccSpan[207];
            ansifl = (char)userAccSpan[208];
            scnwid = (char)userAccSpan[209];
            scnbrk = (char)userAccSpan[210];
            scnfse = (char)userAccSpan[211];
            age = (char)userAccSpan[212];
            sex = (char)userAccSpan[213];
            credat = BitConverter.ToUInt16(userAccSpan.ToArray(), 214);
            usedat = BitConverter.ToUInt16(userAccSpan.ToArray(), 216);
            csicnt = BitConverter.ToInt16(userAccSpan.ToArray(), 218);
            flags = BitConverter.ToInt16(userAccSpan.ToArray(), 220);
            access = userAccSpan.Slice(222, AXSSIZ*2).ToArray(); //14 bytes
            emllim = BitConverter.ToInt32(userAccSpan.ToArray(), 236);
            prmcls = userAccSpan.Slice(240, KEYSIZ).ToArray();
            curcls = userAccSpan.Slice(256, KEYSIZ).ToArray();
            timtdy = BitConverter.ToInt32(userAccSpan.ToArray(), 272);
            daystt = BitConverter.ToUInt16(userAccSpan.ToArray(), 276);
            fgvdys = BitConverter.ToUInt16(userAccSpan.ToArray(), 278);
            creds = BitConverter.ToInt32(userAccSpan.ToArray(), 280);
            totcreds = BitConverter.ToInt32(userAccSpan.ToArray(), 284);
            totpaid = BitConverter.ToInt32(userAccSpan.ToArray(), 288);
            birthd = userAccSpan.Slice(292, DATSIZ).ToArray();
        }

        public ReadOnlySpan<byte> ToSpan() => _usrAccStructBytes;

        /// <summary>
        ///     Takes the specified String Username and saves it to the userid field in the UsrAcc 'struct'
        /// </summary>
        /// <param name="username"></param>
        public void SetUserId(string username)
        {
            var newUserId = new byte[UIDSIZ];
            Array.Copy(Encoding.ASCII.GetBytes(username + "\0"), 0, newUserId, 0, username.Length);
            userid = newUserId;
        }

        public string GetUserId()
        {
            Span<byte> useridSpan = userid;
            return Encoding.ASCII.GetString(useridSpan.Slice(0, Array.IndexOf(userid, (byte)0x0)));
        }
    }
}
