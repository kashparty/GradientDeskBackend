namespace BackpropServer.Models {
    public class UserModel {
        public string username { get; set; }
        public string password { get; set; }
        public string email { get; set; }
    }

    public class AuthorizedUserModel {
        public string username { get; set; }
        public string email { get; set; }
        public string jwt { get; set; }
    }

    public class EditUserModel {
        public string email { get; set; }
        public bool usernameChanged { get; set; }
        public string username { get; set; }
        public bool passwordChanged { get; set; }
        public string password { get; set; }
    }

    public class ResetOrDeleteUserModel {
        public string email { get; set; }
    }
}