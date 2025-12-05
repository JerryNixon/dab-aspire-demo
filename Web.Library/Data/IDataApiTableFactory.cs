namespace Web.Library.Data;

public interface IDataApiTableFactory
{
    IDataApiTable<T> Create<T>() where T : class;
    IDataApiTable<T> Create<T>(string entityName) where T : class;
}
