using System.Threading.Tasks;

namespace FileSharing
{
    public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);
}
