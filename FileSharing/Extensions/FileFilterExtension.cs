namespace Extensions
{
    public static class FileFilterExtension
    {
        public static string GetAppropriateFileFilter(this string fileExtension)
        {
            return fileExtension.Length > 0 ?
                string.Format("{1} files (*{0})|*{0}|All files (*.*)|*.*", fileExtension, fileExtension.Remove(0, 1).ToUpper()) :
                "All files (*.*)|*.*";
        }
    }
}
