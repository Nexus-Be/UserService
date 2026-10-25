namespace User.Application.Interfaces;

using System;
using System.Collections.Generic;
using User.Domain.Entities;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, IEnumerable<string> roles, out DateTime expiresAt);
}
