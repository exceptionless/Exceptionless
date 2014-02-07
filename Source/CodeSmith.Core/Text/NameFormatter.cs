using System;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;

// HenriFormatter

namespace CodeSmith.Core.Text
{
    /// <summary>
    /// Named string formatter.
    /// </summary>
    public static class NameFormatter
    {
        
        private static string OutExpression(object source, string expression)
        {
            string format = "";

            int colonIndex = expression.IndexOf(':');
            if (colonIndex > 0)
            {
                format = expression.Substring(colonIndex + 1);
                expression = expression.Substring(0, colonIndex);
            }

            try
            {
                if (String.IsNullOrEmpty(format))
                    return (DataBinder.Eval(source, expression) ?? "").ToString();

                return DataBinder.Eval(source, expression, "{0:" + format + "}") ?? "";
            }
            catch (HttpException)
            {
                throw new FormatException();
            }
        }

        /// <summary>
        /// Formats the specified input String.
        /// </summary>
        /// <param name="format">The input format String.</param>
        /// <param name="source">The source to replace the formant names with.</param>
        /// <returns>A formatted String.</returns>
        public static string Format(string format, object source)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            var result = new StringBuilder(format.Length * 2);

            using (var reader = new StringReader(format))
            {
                var expression = new StringBuilder();
                int @char = -1;

                State state = State.OutsideExpression;
                do
                {
                    switch (state)
                    {
                        case State.OutsideExpression:
                            @char = reader.Read();
                            switch (@char)
                            {
                                case -1:
                                    state = State.End;
                                    break;
                                case '{':
                                    state = State.OnOpenBracket;
                                    break;
                                case '}':
                                    state = State.OnCloseBracket;
                                    break;
                                default:
                                    result.Append((char)@char);
                                    break;
                            }
                            break;
                        case State.OnOpenBracket:
                            @char = reader.Read();
                            switch (@char)
                            {
                                case -1:
                                    throw new FormatException();
                                case '{':
                                    result.Append('{');
                                    state = State.OutsideExpression;
                                    break;
                                default:
                                    expression.Append((char)@char);
                                    state = State.InsideExpression;
                                    break;
                            }
                            break;
                        case State.InsideExpression:
                            @char = reader.Read();
                            switch (@char)
                            {
                                case -1:
                                    throw new FormatException();
                                case '}':
                                    result.Append(OutExpression(source, expression.ToString()));
                                    expression.Length = 0;
                                    state = State.OutsideExpression;
                                    break;
                                default:
                                    expression.Append((char)@char);
                                    break;
                            }
                            break;
                        case State.OnCloseBracket:
                            @char = reader.Read();
                            switch (@char)
                            {
                                case '}':
                                    result.Append('}');
                                    state = State.OutsideExpression;
                                    break;
                                default:
                                    throw new FormatException();
                            }
                            break;
                        default:
                            throw new InvalidOperationException("Invalid state.");
                    }
                } while (state != State.End);
            }

            return result.ToString();
        }

        #region Nested type: State

        private enum State
        {
            OutsideExpression,
            OnOpenBracket,
            InsideExpression,
            OnCloseBracket,
            End
        }

        #endregion
    }
}