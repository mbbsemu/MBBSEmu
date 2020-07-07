using System;
using System.Collections.Generic;

namespace MBBSEmu.HostProcess.Fsd
{
    public interface IFsdUtility
    {
        /// <summary>
        ///     Parses a Field Spec String for FSD and returns a strongly typed list of the Field Specifications
        /// </summary>
        /// <param name="fieldSpec"></param>
        /// <returns></returns>
        List<FsdFieldSpec> ParseFieldSpecs(ReadOnlySpan<byte> fieldSpec);

        /// <summary>
        ///     Determines the X,Y coordinates of specified template fields by stripping ANSI from the field template
        ///     and parsing each character, each new line increments Y and sets X back to 1.
        ///
        ///     Note: X,Y for ANSI starts at 1,1 being the top left of the terminal (not 0,0)
        /// </summary>
        /// <param name="template"></param>
        /// <param name="status"></param>
        void GetFieldPositions(ReadOnlySpan<byte> template, FsdStatus status);

        /// <summary>
        ///     Takes a Specified List of Answers and applies them to the corresponding Field Specs
        /// </summary>
        /// <param name="answers"></param>
        /// <param name="fields"></param>
        void SetAnswers(List<string> answers, List<FsdFieldSpec> fields);

        /// <summary>
        ///     Parses through the template looking for leading ANSI formatting on field specifications
        /// </summary>
        /// <param name="template"></param>
        /// <param name="status"></param>
        void GetFieldAnsi(ReadOnlySpan<byte> template, FsdStatus status);

        /// <summary>
        ///     Extracts the ANSI formatting from the bytes preceding the field
        ///
        ///     Assumption is bytes immediately preceding field contains ANSI formatting
        /// </summary>
        /// <param name="fieldBytes"></param>
        /// <returns></returns>
        byte[] ExtractFieldAnsi(ReadOnlySpan<byte> fieldBytes);

        /// <summary>
        ///     Takes a memory block of null terminated strings in a double null terminated list and loads them
        ///     into an array.
        /// </summary>
        /// <param name="answerCount"></param>
        /// <param name="answerList"></param>
        /// <returns></returns>
        List<string> ParseAnswers(int answerCount, ReadOnlySpan<byte> answerList);

        /// <summary>
        ///     Strips ANSI Sequences as well as any character with an ASCII code > 127
        /// </summary>
        /// <param name="inputBuffer"></param>
        /// <returns></returns>
        ReadOnlySpan<byte> StripAnsi(ReadOnlySpan<byte> inputBuffer);

        /// <summary>
        ///     Builds an Answer String from the Specified FsdStatus Object
        /// </summary>
        /// <param name="fsdStatus"></param>
        /// <returns></returns>
        ReadOnlySpan<byte> BuildAnswerString(FsdStatus fsdStatus);
    }
}