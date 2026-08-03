namespace Pharmacy.Desktop.Components.Ui;

public enum UiButtonVariant
{
    Primary,
    Secondary,
    Tertiary,
    Danger
}

public enum UiButtonSize
{
    Small,
    Medium,
    Large
}

public enum UiButtonType
{
    Button,
    Submit,
    Reset
}

public enum UiBadgeVariant
{
    Neutral,
    Information,
    Success,
    Warning,
    Danger
}

public enum UiCardDensity
{
    Comfortable,
    Compact
}

public enum UiAlertVariant
{
    Information,
    Success,
    Warning,
    Danger
}

public enum UiTextFieldType
{
    Text,
    Search,
    Password,
    Number
}

public enum UiSpinnerSize
{
    Small,
    Medium
}

public enum UiIconName
{
    Home,
    Sales,
    Catalog,
    Inventory,
    Purchasing,
    Cash,
    Customers,
    Reporting,
    Administration,
    Diagnostic,
    UiLab,
    Chevron,
    Menu,
    Lock
}

public sealed record UiSelectOption(string Value, string Label);
