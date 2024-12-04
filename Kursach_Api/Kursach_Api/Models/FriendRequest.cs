using System;
using System.Collections.Generic;

namespace Kursach_Api.Models;

public partial class FriendRequest
{
    public int Id { get; set; }

    public string UserFrom { get; set; } = null!;

    public string UserTo { get; set; } = null!;
}
