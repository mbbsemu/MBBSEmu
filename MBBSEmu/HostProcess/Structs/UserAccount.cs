using System;
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
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(0, UIDSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 0, value.Length);
        }

        /// <summary>
        ///     password
        /// </summary>
        public byte[] psword
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(30, PSWSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 30, value.Length);
        }

        /// <summary>
        ///     user name
        /// </summary>
        public byte[] usrnam
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(40, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 40, value.Length);
        }

        /// <summary>
        ///     address line 1 (company)
        /// </summary>
        public byte[] usrad1
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(70, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 70, value.Length);
        }

        /// <summary>
        ///     address line 2  
        /// </summary>
        public byte[] usrad2
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(100, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 100, value.Length);
        }

        /// <summary>
        ///     address line 3
        /// </summary>
        public byte[] usrad3
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(130, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 130, value.Length);
        }

        /// <summary>
        ///     address line 4
        /// </summary>
        public byte[] usrad4
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(160, NADSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 160, value.Length);
        }

        /// <summary>
        ///     phone number
        /// </summary>
        public byte[] usrpho
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(190, PHOSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 190, value.Length);
        }

        /// <summary>
        ///     system type code
        /// </summary>
        public byte systyp
        {
            get => Data[206];
            set => Data[206] = value;
        }

        /// <summary>
        ///     user preference flags
        /// </summary>
        public byte usrprf
        {
            get => Data[207];
            set => Data[207] = value;
        }

        /// <summary>
        ///     ANSI flags
        /// </summary>
        public byte ansifl
        {
            get => Data[208];
            set => Data[208] = value;
        }

        /// <summary>
        ///     screen width in columns
        /// </summary>
        public byte scnwid
        {
            get => Data[209];
            set => Data[209] = value;
        }

        /// <summary>
        ///     screen length for page breaks
        /// </summary>
        public byte scnbrk
        {
            get => Data[210];
            set => Data[210] = value;
        }

        /// <summary>
        ///     screen length for FSE stuff
        /// </summary>
        public byte scnfse
        {
            get => Data[211];
            set => Data[211] = value;
        }

        /// <summary>
        ///     user's age
        /// </summary>
        public byte age
        {
            get => Data[212];
            set => Data[212] = value;
        }

        /// <summary>
        ///     user's sex ('M' or 'F')
        /// </summary>
        public byte sex
        {
            get => Data[213];
            set => Data[213] = value;
        }

        /// <summary>
        ///     account creation date
        /// </summary>
        public ushort credat
        {
            get => BitConverter.ToUInt16(Data, 214);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 214, 2);
        }

        /// <summary>
        ///     date of last use of account
        /// </summary>
        public ushort usedat
        {
            get => BitConverter.ToUInt16(Data, 216);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 216, 2);
        }

        /// <summary>
        ///     classified-ad counts used so far
        /// </summary>
        public short csicnt
        {
            get => BitConverter.ToInt16(Data, 218);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 218, 2);
        }

        /// <summary>
        ///     various saved bit flags
        /// </summary>
        public short flags
        {
            get => BitConverter.ToInt16(Data, 220);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 220, 2);
        }

        /// <summary>
        ///     array of remote sysop access bits
        /// </summary>
        public byte[] access
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(222, AXSSIZ * 2).ToArray();
            }
            set => Array.Copy(value, 0, Data, 222, AXSSIZ * 2);
        }

        /// <summary>
        ///     e-mail limit reached so far (new/old bdy)
        /// </summary>
        public int emllim
        {
            get => BitConverter.ToInt32(Data, 236);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 236, 4);
        }

        /// <summary>
        ///     class to return user to if necessary
        /// </summary>
        public byte[] prmcls
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(240, KEYSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 240, KEYSIZ);
        }

        /// <summary>
        ///     current class of this user
        /// </summary>
        public byte[] curcls
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(256, KEYSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 256, KEYSIZ);
        }

        /// <summary>
        ///     time user has been online today (in secs)
        /// </summary>
        public int timtdy
        {
            get => BitConverter.ToInt32(Data, 272);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 272, 4);
        }

        /// <summary>
        ///     days left in this class (if applicable)
        /// </summary>
        public ushort daystt
        {
            get => BitConverter.ToUInt16(Data, 274);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 274, 2);
        }

        /// <summary>
        ///     days since debt was last "forgiven"
        /// </summary>
        public ushort fgvdys
        {
            get => BitConverter.ToUInt16(Data, 278);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 278, 2);
        }

        /// <summary>
        ///     credits available or debt (if negative)
        /// </summary>
        public int creds
        {
            get => BitConverter.ToInt32(Data, 280);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 280, 4);
        }

        /// <summary>
        ///     total credits ever posted (paid & free)
        /// </summary>
        public int totcreds
        {
            get => BitConverter.ToInt32(Data, 284);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 284, 4);
        }

        /// <summary>
        ///     total credits ever posted (paid only)
        /// </summary>
        public int totpaid
        {
            get => BitConverter.ToInt32(Data, 288);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 288, 4);
        }

        /// <summary>
        ///     this user's birthday date
        /// </summary>
        public byte[] birthd
        {
            get
            {
                ReadOnlySpan<byte> userAccSpan = Data;
                return userAccSpan.Slice(292, DATSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 292, DATSIZ);
        }

        public readonly byte[] Data = new byte[Size];

        public const ushort Size = 338;

        public UserAccount()
        {
            flags = 1; //Set everyone to having "MASTER" key
            ansifl = 0x1; //Set everyone to ANSI enabled
            sex = (byte)'M';  //Set everyone to male for now
            creds = 31337;
            scnwid = 80; //Screen Width to 80 Columns
            scnfse = 24; //Screen Height to 24 Lines
            scnbrk = 24; //Screen Page Break Height
            Array.Copy(BitConverter.GetBytes((ushort) 1), 0, access, 0, 2);
        }

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
