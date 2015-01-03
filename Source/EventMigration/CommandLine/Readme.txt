//////////////////////////////////////////////////////////////////////////////
//    Command Line Argument Parser
//    ----------------------------
//
//    Author: peterhal@microsoft.com
//
//    Shared Source License for Command Line Parser Library
//
//    This license governs use of the accompanying software ('Software'), and your
//    use of the Software constitutes acceptance of this license.
//
//    You may use the Software for any commercial or noncommercial purpose,
//    including distributing derivative works.
//
//    In return, we simply require that you agree:
//
//    1. Not to remove any copyright or other notices from the Software. 
//    2. That if you distribute the Software in source code form you do so only
//    under this license (i.e. you must include a complete copy of this license
//    with your distribution), and if you distribute the Software solely in
//    object form you only do so under a license that complies with this
//    license.
//    3. That the Software comes "as is", with no warranties. None whatsoever.
//    This means no express, implied or statutory warranty, including without
//    limitation, warranties of merchantability or fitness for a particular
//    purpose or any warranty of title or non-infringement. Also, you must pass
//    this disclaimer on whenever you distribute the Software or derivative
//    works.
//    4. That no contributor to the Software will be liable for any of those types
//    of damages known as indirect, special, consequential, or incidental
//    related to the Software or this license, to the maximum extent the law
//    permits, no matter what legal theory it’s based on. Also, you must pass
//    this limitation of liability on whenever you distribute the Software or
//    derivative works.
//    5. That if you sue anyone over patents that you think may apply to the
//    Software for a person's use of the Software, your license to the Software
//    ends automatically.
//    6. That the patent rights, if any, granted in this license only apply to the
//    Software, not to any derivative works you make.
//    7. That the Software is subject to U.S. export jurisdiction at the time it
//    is licensed to you, and it may be subject to additional export or import
//    laws in other places.  You agree to comply with all such laws and
//    regulations that may apply to the Software after delivery of the software
//    to you.
//    8. That if you are an agency of the U.S. Government, (i) Software provided
//    pursuant to a solicitation issued on or after December 1, 1995, is
//    provided with the commercial license rights set forth in this license,
//    and (ii) Software provided pursuant to a solicitation issued prior to
//    December 1, 1995, is provided with “Restricted Rights” as set forth in
//    FAR, 48 C.F.R. 52.227-14 (June 1987) or DFAR, 48 C.F.R. 252.227-7013
//    (Oct 1988), as applicable.
//    9. That your rights under this License end automatically if you breach it in
//    any way.
//    10.That all rights not expressly granted to you in this license are reserved.
//
//    Usage
//    -----
//
//    Parsing command line arguments to a console application is a common problem. 
//    This library handles the common task of reading arguments from a command line 
//    and filling in the values in a type.
//
//    To use this library, define a class whose fields represent the data that your 
//    application wants to receive from arguments on the command line. Then call 
//    CommandLine.ParseArguments() to fill the object with the data 
//    from the command line. Each field in the class defines a command line argument. 
//    The type of the field is used to validate the data read from the command line. 
//    The name of the field defines the name of the command line option.
//
//    The parser can handle fields of the following types:
//
//    - string
//    - int
//    - uint
//    - bool
//    - enum
//    - array of the above type
//
//    For example, suppose you want to read in the argument list for wc (word count). 
//    wc takes three optional boolean arguments: -l, -w, and -c and a list of files.
//
//    You could parse these arguments using the following code:
//
//    class WCArguments
//    {
//        public bool lines;
//        public bool words;
//        public bool chars;
//        public string[] files;
//    }
//
//    class WC
//    {
//        static void Main(string[] args)
//        {
//            if (CommandLine.ParseArgumentsWithUsage(args, parsedArgs))
//            {
//            //     insert application code here
//            }
//        }
//    }
//
//    So you could call this aplication with the following command line to count 
//    lines in the foo and bar files:
//
//        wc.exe /lines /files:foo /files:bar
//
//    The program will display the following usage message when bad command line 
//    arguments are used:
//
//        wc.exe -x
//
//    Unrecognized command line argument '-x'
//        /lines[+|-]                         short form /l
//        /words[+|-]                         short form /w
//        /chars[+|-]                         short form /c
//        /files:<string>                     short form /f
//        @<file>                             Read response file for more options
//
//    That was pretty easy. However, you realy want to omit the "/files:" for the 
//    list of files. The details of field parsing can be controled using custom 
//    attributes. The attributes which control parsing behaviour are:
//
//    ArgumentAttribute 
//        - controls short name, long name, required, allow duplicates, default value
//        and help text
//    DefaultArgumentAttribute 
//        - allows omition of the "/name".
//        - This attribute is allowed on only one field in the argument class.
//
//    So for the wc.exe program we want this:
//
//    using System;
//    using Utilities;
//
//    class WCArguments
//    {
//        [Argument(ArgumentType.AtMostOnce, HelpText="Count number of lines in the input text.")]
//        public bool lines;
//        [Argument(ArgumentType.AtMostOnce, HelpText="Count number of words in the input text.")]
//        public bool words;
//        [Argument(ArgumentType.AtMostOnce, HelpText="Count number of chars in the input text.")]
//        public bool chars;
//        [DefaultArgument(ArgumentType.MultipleUnique, HelpText="Input files to count.")]
//        public string[] files;
//    }
//
//    class WC
//    {
//        static void Main(string[] args)
//        {
//            WCArguments parsedArgs = new WCArguments();
//            if (CommandLine.ParseArgumentsWithUsage(args, parsedArgs))
//            {
//            //     insert application code here
//            }
//        }
//    }
//
//
//
//    So now we have the command line we want:
//
//        wc.exe /lines foo bar
//
//    This will set lines to true and will set files to an array containing the 
//    strings "foo" and "bar".
//
//    The new usage message becomes:
//
//        wc.exe -x
//
//    Unrecognized command line argument '-x'
//    /lines[+|-]  Count number of lines in the input text. (short form /l)
//    /words[+|-]  Count number of words in the input text. (short form /w)
//    /chars[+|-]  Count number of chars in the input text. (short form /c)
//    @<file>      Read response file for more options
//    <files>      Input files to count. (short form /f)
//
//    If you want more control over how error messages are reported, how /help is 
//    dealt with, etc you can instantiate the CommandLine.Parser class.
//
//
//
//    Cheers,
//    Peter Hallam
//    C# Compiler Developer
//    Microsoft Corp.
//
//
//
//
//    Release Notes
//    -------------
//
//    10/02/2002 Initial Release
//    10/14/2002 Bug Fix
//    01/08/2003 Bug Fix in @ include files
//    10/23/2004 Added user specified help text, formatting of help text to 
//            screen width. Added ParseHelp for /?.
//    11/23/2004 Added support for default values.
//////////////////////////////////////////////////////////////////////////////