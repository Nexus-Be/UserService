namespace User.Application.Interfaces;

using System;
using System.Collections.Generic;
using User.Domain.Entities;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, IEnumerable<string> roles, IEnumerable<string>? permissions, out DateTime expiresAt);
}
