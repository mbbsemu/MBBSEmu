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
        public byte[] userid;

        /// <summary>
        ///     password
        /// </summary>
        public byte[] psword;

        /// <summary>
        ///     user name
        /// </summary>
        public byte[] usrnam;

        /// <summary>
        ///     address line 1 (company)
        /// </summary>
        public byte[] usrad1;

        /// <summary>
        ///     address line 2  
        /// </summary>
        public byte[] usrad2;

        /// <summary>
        ///     address line 3
        /// </summary>
        public byte[] usrad3;

        /// <summary>
        ///     address line 4
        /// </summary>
        public byte[] usrad4;

        /// <summary>
        ///     phone number
        /// </summary>
        public byte[] usrpho;

        /// <summary>
        ///     system type code
        /// </summary>
        public char systyp;

        /// <summary>
        ///     user preference flags
        /// </summary>
        public char usrprf;

        /// <summary>
        ///     ANSI flags
        /// </summary>
        public char ansifl;

        /// <summary>
        ///     screen width in columns
        /// </summary>
        public char scnwid;

        /// <summary>
        ///     screen length for page breaks
        /// </summary>
        public char scnbrk;

        /// <summary>
        ///     screen length for FSE stuff
        /// </summary>
        public char scnfse;

        /// <summary>
        ///     user's age
        /// </summary>
        public char age;

        /// <summary>
        ///     user's sex ('M' or 'F')
        /// </summary>
        public char sex;

        /// <summary>
        ///     account creation date
        /// </summary>
        public ushort credat;

        /// <summary>
        ///     date of last use of account
        /// </summary>
        public ushort usedat;

        /// <summary>
        ///     classified-ad counts used so far
        /// </summary>
        public short csicnt;

        /// <summary>
        ///     various saved bit flags
        /// </summary>
        public short flags;

        /// <summary>
        ///     array of remote sysop access bits
        /// </summary>
        public short[] access;

        /// <summary>
        ///     e-mail limit reached so far (new/old bdy)
        /// </summary>
        public int emllim;

        /// <summary>
        ///     class to return user to if necessary
        /// </summary>
        public byte[] prmcls;

        /// <summary>
        ///     current class of this user
        /// </summary>
        public byte[] curcls;

        /// <summary>
        ///     time user has been online today (in secs)
        /// </summary>
        public int timtdy;

        /// <summary>
        ///     days left in this class (if applicable)
        /// </summary>
        public ushort daystt;

        /// <summary>
        ///     days since debt was last "forgiven"
        /// </summary>
        public ushort fgvdys;

        /// <summary>
        ///     credits available or debt (if negative)
        /// </summary>
        public int creds;

        /// <summary>
        ///     total credits ever posted (paid & free)
        /// </summary>
        public int totcreds;

        /// <summary>
        ///     total credits ever posted (paid only)
        /// </summary>
        public int totpaid;

        /// <summary>
        ///     this user's birthday date
        /// </summary>
        public byte[] birthd;

        /// <summary>
        ///     spare space, for graceful upgrades
        /// </summary>
        public byte[] spare;

        private byte[] _usrAccStructBytes;

        public UserAccount()
        {
            _usrAccStructBytes = new byte[338];
            userid = new byte[UIDSIZ];
            psword = new byte[PSWSIZ];
            usrnam = new byte[NADSIZ];
            usrad1 = new byte[NADSIZ];
            usrad2 = new byte[NADSIZ];
            usrad3 = new byte[NADSIZ];
            usrad4 = new byte[NADSIZ];
            systyp = '\0';
            usrprf = '\0';
            ansifl = '\0';
            scnwid = '\0';
            scnbrk = '\0';
            scnfse = '\0';
            age = '\0';
            sex = '\0';
            credat = 0;
            usedat = 0;
            csicnt = 0;
            flags = 0;
            access = new short[AXSSIZ];
            emllim = 0;
            prmcls = new byte[KEYSIZ];
            curcls = new byte[KEYSIZ];
            timtdy = 0;
            daystt = 0;
            fgvdys = 0;
            creds = 0;
            totcreds = 0;
            totpaid = 0;
            birthd = new byte[DATSIZ];
            spare = new byte[USRACCSPARE];
        }

        public void FromSpan(ReadOnlySpan<byte> userAccSpan)
        {
            userid = userAccSpan.Slice(0, UIDSIZ).ToArray();
            psword = userAccSpan.Slice(30, PSWSIZ).ToArray();
            usrnam = userAccSpan.Slice(40, NADSIZ).ToArray();
        }

        public ReadOnlySpan<byte> ToSpan()
        {
            using var msOutput = new MemoryStream();
            msOutput.Write(userid);
            msOutput.Write(psword);
            msOutput.Write(usrnam);
            _usrAccStructBytes = msOutput.ToArray();
            return _usrAccStructBytes;
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
