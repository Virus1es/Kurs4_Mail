import {useNavigate} from "react-router-dom";
import {useCookies} from "react-cookie";
import {useEffect, useLayoutEffect} from "react";
import {isUndefined} from "swr/_internal";

export default function ShowHome(){

    const navigate = useNavigate();

    const [cookies, removeCookie] = useCookies(['currentUser']);

    const checkUser = () => {
        if(isUndefined(localStorage.getItem('userCredentials'))) {
            navigate('/login');
        }
    }

    // если пользователь не зашёл в аккаунт заставляем его зайти
    useEffect(() => {
        checkUser();
    }, [cookies.currentUser]);

    return(
        <div>
            <h1>Главная страница</h1>
        </div>
    );
}