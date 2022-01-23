using Newtonsoft.Json;

namespace FileSharing.Utils
{
    public static class JSON<T>
    {
        public static string ToJSON(T obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static T FromJSON(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
