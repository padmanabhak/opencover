﻿// Copyright (c) https://github.com/ddur
// This code is distributed under MIT license

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using OpenCover.Framework.Model;

namespace OpenCover.Framework.Utility
{
    /// <summary>
    /// FileType enum
    /// </summary>
    public enum FileType : byte {
        /// <summary>
        /// Unsupported file extension
        /// </summary>
        Unsupported,

        /// <summary>
        /// File extension is ".cs"
        /// </summary>
        CSharp

    }
    /// <summary>StringTextSource (ReadOnly)
    /// <remarks>Line and column counting starts at 1.</remarks>
    /// </summary>
    public class CodeCoverageStringTextSource
    {
        /// <summary>
        /// File Type guessed by file-name extension
        /// </summary>
        public FileType FileType { get { return fileType; } }
        private readonly FileType fileType = FileType.Unsupported;

        /// <summary>
        /// Path to source file
        /// </summary>
        public string FilePath { get { return filePath; } }
        private readonly string filePath = string.Empty;

        /// <summary>
        /// Source file found or not
        /// </summary>
        public bool FileFound { get { return fileFound; } }
        private readonly bool fileFound = false;

        /// <summary>
        /// Last write DateTime
        /// </summary>
        public DateTime FileTime { get { return timeStamp; } }
        private readonly DateTime timeStamp = new DateTime();

        private readonly string textSource;

        private struct lineInfo {
            public int Offset;
            public int Length;
        }
        private readonly lineInfo[] lines = new lineInfo[0];

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="source"></param>
        /// <param name="filePath"></param>
        public CodeCoverageStringTextSource(string source, string filePath = "")
        {
            this.fileFound = source != null;

            if (!string.IsNullOrWhiteSpace (filePath)) {
                this.filePath = filePath;
                if (this.filePath.IndexOfAny(Path.GetInvalidPathChars()) < 0
                    && Path.GetExtension(this.filePath).ToLowerInvariant() == ".cs" ) {
                    this.fileType = FileType.CSharp;
                }
                if (this.fileFound) {
                    try { timeStamp = System.IO.File.GetLastWriteTime (this.filePath); } catch {}
                }

            }

            if (string.IsNullOrEmpty(source)) {
                this.textSource = string.Empty;
            } else {
                this.textSource = source;
            }

            lineInfo line;
            var lineInfoList = new List<lineInfo>();
            int offset = 0;
            int counter = 0;
            bool newLine = false;
            bool cr = false;
            bool lf = false;
            const ushort carriageReturn = 0xD;
            const ushort lineFeed = 0xA;

            if (textSource != string.Empty) {
                foreach ( ushort ch in textSource ) {
                    switch (ch) {
                        case carriageReturn:
                            if (lf||cr) {
                                lf = false;
                                newLine = true; // cr after cr|lf
                            } else {
                                cr = true; // cr found
                            }
                            break;
                        case lineFeed:
                            if (lf) {
                                newLine = true; // lf after lf
                            } else {
                                lf = true; // lf found
                            }
                            break;
                        default:
                            if (cr||lf) {
                                cr = false;
                                lf = false;
                                newLine = true; // any non-line-end char after any line-end
                            }
                            break;
                    }
                    if (newLine) { // newLine detected - add line
                        newLine = false;
                        line = new lineInfo();
                        line.Offset = offset;
                        line.Length = counter - offset;
                        lineInfoList.Add(line);
                        offset = counter;
                    }
                    ++counter;
                }
                
                // Add last line
                line = new lineInfo();
                line.Offset = offset;
                line.Length = counter - offset;
                lineInfoList.Add(line);
    
                // Store to readonly field
                lines = lineInfoList.ToArray();
            }
        }

        /// <summary>Return text/source using SequencePoint line/col info
        /// </summary>
        /// <param name="sp"></param>
        /// <returns></returns>
        public string GetText(SequencePoint sp) {
            return this.GetText(sp.StartLine, sp.StartColumn, sp.EndLine, sp.EndColumn );
        }

        /// <summary>Return text at Line/Column/EndLine/EndColumn position
        /// <remarks>Line and Column counting starts at 1.</remarks>
        /// </summary>
        /// <param name="Line"></param>
        /// <param name="Column"></param>
        /// <param name="EndLine"></param>
        /// <param name="EndColumn"></param>
        /// <returns></returns>
        public string GetText(int Line, int Column, int EndLine, int EndColumn) {

            var text = new StringBuilder();
            string line;
            bool argOutOfRange;

            if (Line==EndLine) {

                #region One-Line request
                line = GetLine(Line);

                argOutOfRange = Column > EndColumn || Column > line.Length;
                if (!argOutOfRange) {
                    if (Column < 1) { Column = 1; }
                    if (EndColumn > line.Length + 1) { EndColumn = line.Length + 1; }
                    text.Append(line.Substring (Column-1, EndColumn-Column));
                }
                #endregion

            } else if (Line<EndLine) {

                #region Multi-line request

                #region First line
                line = GetLine(Line);

                argOutOfRange = Column > line.Length;
                if (!argOutOfRange) {
                    if (Column < 1) { Column = 1; }
                    text.Append (line.Substring (Column-1));
                }
                #endregion

                #region More than two lines
                for ( int lineIndex = Line + 1; lineIndex < EndLine; lineIndex++ ) {
                    text.Append ( GetLine ( lineIndex ) );
                }
                #endregion

                #region Last line
                line = GetLine(EndLine);

                argOutOfRange = EndColumn < 1;
                if (!argOutOfRange) {
                    if (EndColumn > line.Length + 1) { EndColumn = line.Length + 1; }
                    text.Append(line.Substring(0,EndColumn-1));
                }
                #endregion

                #endregion

            } else {
                ;
            }
            return text.ToString();
        }

        /// <summary>
        /// Return number of lines in source
        /// </summary>
        public int LinesCount {
            get {
                return lines.Length;
            }
        }

        /// <summary>Return SequencePoint enumerated line
        /// </summary>
        /// <param name="LineNo"></param>
        /// <returns></returns>
        public string GetLine ( int LineNo ) {

            string retString = String.Empty;

            if ( LineNo > 0 && LineNo <= lines.Length ) {
                lineInfo lineInfo = lines[LineNo-1];
                retString = textSource.Substring(lineInfo.Offset, lineInfo.Length);
            }

            return retString;
        }
        
        /// <summary>
        /// Get line-parsed source from file name
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static CodeCoverageStringTextSource GetSource(string filePath) {

            var retSource = new CodeCoverageStringTextSource (null, filePath); // null indicates source-file not found
            try {
                using (Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new StreamReader (stream, Encoding.Default, true)) {
                    stream.Position = 0;
                    retSource = new CodeCoverageStringTextSource(reader.ReadToEnd(), filePath);
                }
            } catch (Exception e) { 
                // Source is optional (for excess-branch removal), application can continue without it
                LogHelper.InformUser(e); // Do not throw ExitApplicationWithoutReportingException
            }
            return retSource;
        }

    }
}
