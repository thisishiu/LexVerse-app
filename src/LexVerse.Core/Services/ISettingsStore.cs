using LexVerse.Core.Models;

namespace LexVerse.Core.Services;

public interface ISettingsStore
{
    AppSettings Load();
}
