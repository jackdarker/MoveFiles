using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MoveFiles
{
    class Program
    {
        class FileMover
        {
            public string m_Destination = "";
            public string m_Source = "";
            private bool m_LogtoFile = false;
            public bool m_DeleteOnly = false;
            public bool m_DeleteSubDir = false;
            public double m_OlderThanDays = -1;
            public double m_OlderThanHours = -1;
            public int m_DebugLevel = 0;    //0=Off; bit0=processsed folders; bit1=moved files; bit2=skipped files; bit3=error details;bit7=log to file
            public int m_Errors = 0;
            private DirectoryInfo m_SourceDir = null;
            private DirectoryInfo m_DestinationDir = null;
            private FileStream m_LogFile;

            public FileMover()
            { }

            public void Run()
            {
                m_LogtoFile = ((m_DebugLevel & 0x80) != 0);
                if(m_LogtoFile) {
                    string path = (Directory.GetCurrentDirectory() + "\\" +
                        DateTime.Now.Ticks.ToString() + ".log");
                    m_LogFile = File.Create(path);
                }
                if (Directory.Exists(m_Source))
                {
                    m_SourceDir = new DirectoryInfo(m_Source);
                    if (!m_DeleteOnly) {
                        m_DestinationDir = CreateDestination(m_Destination);
                        if (m_DestinationDir == null)
                        { //error already handled in function     
                            return;
                        }
                    }
                    ProcessDirectory(m_SourceDir, m_DestinationDir, "");
                }
                LogError(string.Format("finished with {0:d} errors", m_Errors));
                if(this.m_LogFile!=null) this.m_LogFile.Close();
            }
            private bool ProcessDirectory(DirectoryInfo RootDir,DirectoryInfo DestDir, string RelSourceDir)
            {
                bool Result = false; //true means Processing was finished correctly
                if(Directory.Exists(RootDir.FullName +"\\"+ RelSourceDir))
                {
                    DirectoryInfo _SourceDir = new DirectoryInfo(RootDir.FullName + "\\" + RelSourceDir);
                    DirectoryInfo _DestinationDir = m_DeleteOnly ? null : CreateDestination(DestDir.FullName + "\\" + RelSourceDir);
                   
                    if ((m_DebugLevel & 0x1)!=0) LogError(_SourceDir.FullName);
                    if (m_DeleteOnly || _DestinationDir != null)
                    {
                        FileInfo[] Files = GetFilesToMove(_SourceDir);
                        foreach (FileInfo _File in Files)
                        {
                            try
                            {
                                if((m_DebugLevel & 0x2) != 0) {
                                    LogError(_File.Name);
                                }
                                if (!m_DeleteOnly) {
                                    string _sDest = _DestinationDir.FullName + "\\" + _File.Name;
                                    _File.CopyTo(_sDest, true);
                                    FileInfo fi1 = new FileInfo(_sDest);
                                    
                                    if(!fi1.Exists || fi1.Length != _File.Length) {
                                        throw new Exception("file not completely copied");
                                    }

                                }
                                _File.Delete();
                            }
                            catch (Exception ex)
                            {
                                LogError(ex.Message);
                                m_Errors++;
                            }
                        }
                        Files = null; //
                        DirectoryInfo[] Dirs = GetSubDirs(_SourceDir);
                        string _NextDir = "";
                        foreach (DirectoryInfo _Dir in Dirs)
                        {
                            _NextDir = RelSourceDir + "\\" + _Dir.Name;
                            ProcessDirectory(RootDir, DestDir, _NextDir); //recursively calling itself
                            if (m_DeleteSubDir) {
                                DeleteDirectory(_Dir);
                            }
                        }

                        Dirs = null;
                    };
                };
                return Result;
            }
            private void LogError(string message) {
                if(m_LogtoFile) {
                    byte[] info = new UTF8Encoding(true).GetBytes(message+"\r\n");
                    m_LogFile.Write(info, 0, info.Length);
                    
                }
                Console.WriteLine( message); //
            }
            private bool DeleteDirectory(DirectoryInfo Dir) {
                bool Result = false; //true means Processing was finished correctly
                try {
                    if (Dir.Exists) {
                        //delete only if there are no files or subdirectories
                        if (Dir.GetFileSystemInfos().Length > 0) {
                        } else {
                            Dir.Delete();
                            Result = true;
                        }
                    } else {
                        return true;
                    }
                } catch (Exception ex) {
                    if((m_DebugLevel & 0x8) != 0) {
                        LogError(ex.Message + " skipped");
                    }
                }
                return Result;
            }
            private DirectoryInfo[] GetSubDirs(DirectoryInfo Dir)
            {
                return Dir.GetDirectories();
            }
            private FileInfo[] GetFilesToMove(DirectoryInfo Dir)
            {
                FileInfo[] _Files = Dir.GetFiles();
                int _NumberOfFiles = 0;
                int[] FileOK = new int[_Files.GetLength(0)];
                for (int i = 0; i < _Files.GetLength(0); i++) {
                    if ((m_OlderThanDays < 0 && m_OlderThanHours < 0 )) {
                        //delete all
                        FileOK[_NumberOfFiles] = i;
                        _NumberOfFiles++;
                    } else if (m_OlderThanDays > 0  &&
                        (DateTime.Now - _Files[i].LastWriteTime).TotalDays >= m_OlderThanDays) {
                        FileOK[_NumberOfFiles] = i;
                        _NumberOfFiles++;
                    } else if (m_OlderThanHours > 0  &&
                        (DateTime.Now - _Files[i].LastWriteTime).TotalHours >= m_OlderThanHours) {
                        FileOK[_NumberOfFiles] = i;
                        _NumberOfFiles++;
                    } else if ((m_DebugLevel & 0x4) != 0) {
                        LogError("skipped "+_Files[i].Name);
                    }
                }
                FileInfo[] _FilteredFiles = new FileInfo[_NumberOfFiles];
                for (int i = 0; i < _NumberOfFiles; i++) {
                    _FilteredFiles[i] = _Files[FileOK[i]];
                }

                return _FilteredFiles;
            }
            private DirectoryInfo CreateDestination(string Destination)
            {
                DirectoryInfo Result = null;
                try{
             if (Directory.Exists(Destination))
             {
                    Result = new DirectoryInfo(Destination);
             }
             else
             {
                    Result = Directory.CreateDirectory(Destination);
             }
             }
                catch(Exception ex)
                {
                    LogError("Cannot create or access destination or subdirectory");
                    m_Errors++;
             }
                
                return Result;
            }
        }
        static void Main(string[] args)
        {

            bool bShowHelp = false;
            string strDestination ="";
            string strSource ="";
            string strError ="";
            FileMover _Mover = new FileMover();

          try
          {
            foreach (string arg in args)
            {
                if(arg.ToLower()=="/help" || 
                    arg.ToLower()=="-?" ||
                    arg.ToLower()=="/?")
                {
                    bShowHelp = true;
                }

                else if(arg.ToLower().Substring(0,1) == "/")
                {
                    if(arg.ToLower().Substring(1,1) == "o")
                    {
                        try {

                        if (arg.ToLower().Substring(2, 1) == "h") {
                            _Mover.m_OlderThanHours = Convert.ToDouble(arg.Substring(3));
                        } else {
                            _Mover.m_OlderThanDays = Convert.ToDouble(arg.Substring(2));
                            //_Mover.m_OlderThanDays = Convert.ToInt32(arg.Substring(2));
                        }
                                                   
                        }
                        catch(FormatException ex)
                        {
                            strError += "not a number: " + arg+ "\r\n";
                        }
                    } 
                    else if (arg.ToLower().Substring(1, 1) == "c") {
                        _Mover.m_DeleteOnly = true;
                    } else if(arg.ToLower().Substring(1, 1) == "f") {
                        _Mover.m_DeleteSubDir = true;
                    } else if (arg.ToLower().Substring(1, 1) == "d"){
                        try
                        {
                            _Mover.m_DebugLevel = Convert.ToInt32(arg.Substring(2));
                        }
                        catch(FormatException ex)
                        {
                            strError += "not a number: " + arg+ "\r\n";
                        }
                    }
                    else
                    {
                        strError+="unknown argument: "+ arg + "\r\n";
                    }
                }
                else
                {
                    if(strSource == "")
                    {
                        strSource = arg;
                    }
                    else
                    {
                        strDestination =arg;
                    }
                }
            }
            if (bShowHelp) {
                PrintHelp();
            } else {
                if (strSource == "") strError += "no source specified \r\n";
                if (strDestination == "" && !_Mover.m_DeleteOnly) strError += "no destination specified \r\n";
                if (strDestination != "" && _Mover.m_DeleteOnly) strError += "destination specified but Delete-Flag set\r\n";
                if (strError != "")
                {
                    strError += "use /? for help \r\n";
                    throw new ArgumentException(strError);
                }
                else
                {
                    
                    _Mover.m_Destination = strDestination;
                    _Mover.m_Source = strSource;
                    _Mover.Run();
                }
            }

          }


          catch (ArgumentException ex)
          {
            Console.Write(ex.Message);

          }
          finally
          {

          }
        }
        static void PrintHelp()
        {
            Console.WriteLine("Moves files from one directory to another. Will also create subdirectorys.");
            Console.WriteLine("       JKubik 16/05");
            Console.WriteLine();
            Console.WriteLine("MoveFiles [/C] [/F] [/Od] [/Dd] source destination");
            Console.WriteLine();
            Console.WriteLine("source         directory to move.");
            Console.WriteLine("destination    directory where to move the content of source");
            Console.WriteLine("/C             dont move, juste delete the files; destination has to be empty");
            Console.WriteLine("/F             delete empty subdirectorys after delete or move");
            Console.WriteLine("/Od            move only files older than d days;d can be floating point number");
            Console.WriteLine("/Ohd            move only files older than d hours;d can be floating point number");
            Console.WriteLine("/Dd            debugswitch (slows down processing) where d=");
            Console.WriteLine("                bit0=print processsed folders; bit1=print moved files; bit2=print skipped files;");
            Console.WriteLine("                bit3=print error details; bit7=log to file;");
            Console.WriteLine();
            Console.WriteLine("Creates Destination if doesnt exist.");
            Console.WriteLine("Overwrites existing files. If file is locked, its just copied but not deleted.");
            Console.WriteLine("press any key...");
            Console.ReadKey();
        }
    }
}
