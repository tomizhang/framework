namespace EShop.Domain
{
    // 1. 定义主键接口
    public interface IEntity<TKey>
    {
        TKey Id { get; set; }
    }

    // 2. 定义创建审计接口
    public interface ICreationAudited
    {
        DateTime CreationTime { get; set; }
        long? CreatorId { get; set; } // 使用 ID 引用用户，可空（因为可能是系统自动创建）
    }

    // 3. 定义软删除接口
    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
    }

 
}
