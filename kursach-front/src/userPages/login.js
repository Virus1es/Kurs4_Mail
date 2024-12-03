import { InputText } from 'primereact/inputtext';
import { Password } from 'primereact/password';
import { Button } from 'primereact/button';
import { FloatLabel } from "primereact/floatlabel";
import {useRef, useState} from "react";
import {useNavigate} from "react-router-dom";
import {useCookies} from "react-cookie";
import {Toast} from "primereact/toast";
import {isNullOrUndef} from "chart.js/helpers";

// посыл запроса входа в аккаунт на сервер
export function LoginUser(email, password, toast, navigate){

    fetch("http://localhost:5113/mail/TestConnect", {
        method: "POST",
        headers: {
            "Content-Type": "application/json", // Тип содержимого
        },
        body: JSON.stringify({
            Email: email,
            AppPassword: password
        }),
    })
        .then(response => {
        // Обработка ответа от сервера
        if (!response.ok) {
            // Проверка на ошибки HTTP (4xx или 5xx)
            // Создаём ошибку, если ответ не успешный.
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        return response;
    })
        .then(data => {
            // Обработка полученных данных (data содержит JSON)
            console.log("Полученные данные:", data);

            const credentialsJSON = localStorage.getItem('userCredentials');
            let users = credentialsJSON ? JSON.parse(credentialsJSON) : [];

            if(users !== []){
                users.push({email, password});
                localStorage.setItem('userCredentials', JSON.stringify(users));
            }
            else{
                localStorage.setItem('userCredentials', JSON.stringify({email, password}));
            }

            localStorage.setItem('curUser', `${users.length - 1}`);
            navigate('/');
        })
        .catch(error => {
            // Обработка ошибок
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: 'Не верно указан логин или пароль'
            });
        });
}

// вход пользователя в аккаунт
export default function ShowLogin(){
    // для уведомлений Toast
    const toast = useRef(null);

    // почта пользователя
    const [email, setEmail] = useState('');

    // пароль пользователя
    const [password, setPassword] = useState('');

    // используется для redirect
    const navigate = useNavigate();

    return(
        <div className="flex flex-column justify-content-center mt-5">

            <p className="text-4xl font-semibold mx-auto">Вход в аккаунт</p>

            <FloatLabel className="my-3 mx-auto">
                <InputText value={email}
                           id="username"
                           onChange={(e) => setEmail(e.target.value)}
                />
                <label htmlFor="username" style={{fontSize: '12pt', marginTop: '-9px'}}>Почта</label>
            </FloatLabel>

            <FloatLabel className="my-3 mx-auto">
                <Password value={password}
                          id="password"
                          onChange={(e) => setPassword(e.target.value)}
                          toggleMask
                          feedback={false}
                />
                <label htmlFor="password" style={{fontSize: '12pt', marginTop: '-9px'}}>Пароль</label>
            </FloatLabel>

            <Button label="Войти"
                    icon="pi pi-sign-in"
                    className="my-2 mx-auto"
                    onClick={() => {
                        if(email !== '' && password !== '') {
                            LoginUser(email, password, toast, navigate);
                        }
                        else {
                            toast.current.show({
                                severity: 'error',
                                summary: 'Ошибка',
                                detail: 'Введите данные для входа'
                            });
                        }
                    }}
            />
            <Toast ref={toast} />
        </div>
    )
}