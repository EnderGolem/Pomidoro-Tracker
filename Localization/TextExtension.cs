using System.Windows.Data;
using System.Windows.Markup;

namespace Pomidoro.Localization;

// Short syntax for markup: {loc:Text StartButton}.
// A binding is returned rather than a ready string: that way a typo in the key shows up
// as a binding error in the Output window instead of silently becoming an empty label.
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
