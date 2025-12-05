using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EShop.Domain.Common
{
    // 基础实体，只包含 Id
    public abstract class BaseEntity<TKey> : IEntity<TKey>
    {
        public TKey Id { get; set; }

        // 重写 Equals 方法，防止比较对象时出错（DDD 核心：实体比较基于 Id）
        public override bool Equals(object obj)
        {
            if (!(obj is BaseEntity<TKey> other)) return false;

            // 如果 Id 是默认值（如 0 或 null），则视为不相等
            if (ReferenceEquals(this, other)) return true;
            if (EqualityComparer<TKey>.Default.Equals(Id, default)) return false;

            return EqualityComparer<TKey>.Default.Equals(Id, other.Id);
        }

        // 配合 Equals 重写 GetHashCode
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    // 全功能实体（包含审计和软删除）
    // 大部分业务表（如商品、订单）会继承这个
    public abstract class FullAuditedEntity<TKey> : BaseEntity<TKey>, ICreationAudited, ISoftDelete
    {
        public DateTime CreationTime { get; set; } = DateTime.UtcNow; // 给个默认值
        public long? CreatorId { get; set; }
        public bool IsDeleted { get; set; }
    }
}
