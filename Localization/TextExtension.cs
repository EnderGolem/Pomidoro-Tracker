using System.Windows.Data;
using System.Windows.Markup;

namespace Pomidoro.Localization;

// Короткая запись для разметки: {loc:Text StartButton}.
// Возвращается привязка, а не готовая строка: так опечатка в ключе видна
// как ошибка привязки в окне вывода, а не молча становится пустой подписью.
internal sealed class TextExtension : MarkupExtension
{
    public TextExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        Binding binding = new(Key)
        {
            Source = Loc.S,
            Mode = BindingMode.OneTime
        };

        return binding.ProvideValue(serviceProvider);
    }
}
